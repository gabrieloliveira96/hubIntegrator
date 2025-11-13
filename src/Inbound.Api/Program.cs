using Inbound.Api.Application.Behaviors;
using Inbound.Api.Consumers;
using Inbound.Api.Endpoints;
using Inbound.Api.Infrastructure.Messaging;
using Inbound.Api.Infrastructure.Persistence;
using Inbound.Api.Infrastructure.Persistence.Repositories;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using Shared.Observability;
using Shared.Persistence;
using Shared.Security;
using Shared.Web;
using System.Diagnostics;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Serilog
var seqUrl = builder.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341";
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .Enrich.WithEnvironmentName()
    .Enrich.WithProperty("Service", "Inbound.Api")
    .Enrich.WithProperty("Version", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] [{Service}] {Message:lj}{NewLine}  Properties: {Properties:j}{NewLine}  {Exception}")
    .WriteTo.Seq(seqUrl)
    .CreateLogger();

builder.Host.UseSerilog();

// Services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Inbound API",
        Version = "v1",
        Description = "API para recebimento de requisições de parceiros. " +
                     "Require headers obrigatórios: Idempotency-Key, X-Nonce, X-Timestamp. " +
                     "Require autenticação JWT via IdentityServer."
    });
    
    // Configurar autenticação JWT Bearer
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header usando o esquema Bearer. Exemplo: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT"
    });
    
    // Aplicar segurança globalmente
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
    
    // Adicionar headers automaticamente no Swagger
    c.OperationFilter<Inbound.Api.Infrastructure.Swagger.RequiredHeadersOperationFilter>();
});

// Database
builder.Services.AddDbContext<InboxDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSQL")));

// Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    return ConnectionMultiplexer.Connect(connectionString);
});

// MassTransit
builder.Services.AddMassTransit(x =>
{
    // Register consumers
    x.AddConsumer<RequestStatusUpdateConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", "/", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });

        cfg.ConfigureEndpoints(context);
    });
});

// MediatR
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
});

// Application Services
builder.Services.AddScoped<IIdempotencyStore, IdempotencyStore>();
builder.Services.AddScoped<IMqPublisher, MqPublisher>();

// Repositories
builder.Services.AddScoped<IRequestRepository, RequestRepository>();
builder.Services.AddScoped<IInboxRepository, InboxRepository>();
builder.Services.AddScoped<IDedupKeyRepository, DedupKeyRepository>();

// MediatR Behaviors
builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

// Authentication & Authorization
// Em desenvolvimento, permitir validação flexível se não houver servidor OAuth2
var allowDevWithoutAuth = builder.Configuration.GetValue<bool>("Jwt:AllowDevelopmentWithoutAuthority", defaultValue: true);
builder.Services.AddJwtAuthentication(
    authority: builder.Configuration["Jwt:Authority"] ?? "https://localhost:5002",
    audience: builder.Configuration["Jwt:Audience"] ?? "hub-api",
    allowDevelopmentWithoutAuthority: allowDevWithoutAuth);

// OpenTelemetry
builder.Services.AddOpenTelemetry(
    serviceName: "inbound-api",
    otlpEndpoint: builder.Configuration["OpenTelemetry:OtlpEndpoint"]);

// Health Checks
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("PostgreSQL") ?? "")
    .AddRedis(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379");

var app = builder.Build();

// Middleware
app.UseSerilogRequestLogging();
app.UseCorrelationMiddleware();
app.UseGlobalErrorHandling();

// Swagger - sempre habilitado em Development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Inbound API v1");
        c.RoutePrefix = "swagger"; // Acesse em /swagger
        c.DisplayRequestDuration();
        c.EnableDeepLinking();
        c.EnableFilter();
        c.EnableValidator();
        c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
        
        // Instruções para obter token
        c.HeadContent = @"
            <style>
                .swagger-ui .topbar { display: none; }
            </style>
            <div style='background: #f0f0f0; padding: 10px; margin-bottom: 20px; border-radius: 5px;'>
                <strong>🔑 Como obter um Token JWT:</strong><br/>
                1. Acesse o IdentityServer: <a href='https://localhost:5002/connect/token' target='_blank'>https://localhost:5002/connect/token</a><br/>
                2. Use Client Credentials: client_id=hub-client, client_secret=hub-secret<br/>
                3. Cole o access_token no botão 'Authorize' acima
            </div>
        ";
    });
}

// Anti-Replay deve vir depois do Swagger para não bloquear
app.UseAntiReplay();

app.UseAuthentication();
app.UseAuthorization();

// Endpoints
app.MapRequestsEndpoints();

// Health
app.MapHealthChecks("/healthz");
app.MapHealthChecks("/readyz", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

// Migrations
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<InboxDbContext>();
    db.Database.Migrate();
}

// Pré-carregar metadados JWT em desenvolvimento (síncrono na inicialização)
if (app.Environment.IsDevelopment())
{
    try
    {
        Log.Information("Pré-carregando metadados JWT do IdentityServer...");
        var jwtBearerOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>>();
        var options = jwtBearerOptions.Get(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme);
        
        if (options.ConfigurationManager != null)
        {
            // Forçar carregamento síncrono dos metadados
            var configTask = options.ConfigurationManager.GetConfigurationAsync(CancellationToken.None);
            var config = configTask.GetAwaiter().GetResult(); // Bloquear até carregar
            
            Log.Information("Metadados JWT carregados com sucesso. Issuer: {Issuer}, Chaves JWKS: {KeyCount}", 
                config.Issuer, config.SigningKeys?.Count ?? 0);
            
            if (config.SigningKeys == null || config.SigningKeys.Count == 0)
            {
                Log.Warning("ATENCAO: Nenhuma chave JWKS foi carregada!");
            }
        }
        else
        {
            Log.Warning("ConfigurationManager nao foi configurado!");
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "ERRO CRITICO ao pré-carregar metadados JWT. A autenticacao pode falhar.");
    }
}

Log.Information("Inbound.Api starting on {Urls}", app.Urls);
Log.Information("Environment: {Environment}", app.Environment.EnvironmentName);
Log.Information("Swagger available at: http://localhost:5001/swagger");

app.Run();
