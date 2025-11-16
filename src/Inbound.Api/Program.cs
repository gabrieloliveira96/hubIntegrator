using Inbound.Api.Application.Behaviors;
using Inbound.Api.Consumers;
using Inbound.Api.Domain.Repositories;
using Inbound.Api.Domain.Services;
using Inbound.Api.Infrastructure.Messaging;
using Inbound.Api.Infrastructure.Persistence;
using Inbound.Api.Infrastructure.Persistence.Repositories;
using Inbound.Api.Presentation.Endpoints;
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
                     "Autenticação OIDC e Rate Limiting são gerenciados pelo Gateway."
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
        var rabbitMqConnectionString = builder.Configuration.GetConnectionString("RabbitMQ") ?? "amqp://guest:guest@localhost:5672/";
        var uri = new Uri(rabbitMqConnectionString);
        
        // Virtual host: usar "/" (padrão) se estiver vazio
        var virtualHost = uri.AbsolutePath.TrimStart('/');
        if (string.IsNullOrEmpty(virtualHost))
        {
            virtualHost = "/";
        }
        
        cfg.Host(uri.Host, (ushort)uri.Port, virtualHost, h =>
        {
            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                var credentials = uri.UserInfo.Split(':');
                h.Username(credentials[0]);
                h.Password(credentials.Length > 1 ? credentials[1] : "");
            }
        });

        cfg.ConfigureEndpoints(context);
    });
});

// MediatR
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
});

// Domain Services (implemented in Infrastructure)
builder.Services.AddScoped<IIdempotencyStore, IdempotencyStore>();
builder.Services.AddScoped<IMqPublisher, MqPublisher>();

// Domain Repositories (implemented in Infrastructure)
builder.Services.AddScoped<IRequestRepository, RequestRepository>();
builder.Services.AddScoped<IInboxRepository, InboxRepository>();
builder.Services.AddScoped<IDedupKeyRepository, DedupKeyRepository>();

// MediatR Behaviors
builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

// Authentication & Authorization
// Nota: O Gateway centraliza autenticação OIDC e valida tokens JWT antes de rotear
// O Inbound.Api confia no Gateway - se a requisição chegou aqui, já foi autenticada
// Apenas configuramos JWT se houver Authority (para acesso direto sem Gateway em desenvolvimento)
var allowDevWithoutAuth = builder.Configuration.GetValue<bool>("Jwt:AllowDevelopmentWithoutAuthority", defaultValue: true);
var jwtAuthority = builder.Configuration["Jwt:Authority"];
if (!string.IsNullOrEmpty(jwtAuthority))
{
    builder.Services.AddJwtAuthentication(
        authority: jwtAuthority,
        audience: builder.Configuration["Jwt:Audience"] ?? "hub-api",
        allowDevelopmentWithoutAuthority: allowDevWithoutAuth);
}
else
{
    // Sem Authority configurada = confia totalmente no Gateway
    // Apenas log para indicar que está confiando no Gateway
    Log.Information("Inbound.Api configurado para confiar no Gateway para autenticação");
}

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
        

    });
}

// Anti-Replay deve vir depois do Swagger para não bloquear
app.UseAntiReplay();

// Authentication: Gateway já validou o token JWT antes de rotear
// Apenas usar UseAuthentication() se JWT estiver configurado (para acesso direto sem Gateway)
if (!string.IsNullOrEmpty(jwtAuthority))
{
    app.UseAuthentication();
}
// Authorization removido - Gateway já autorizou (validou token e scopes)

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

// Pré-carregar metadados JWT em desenvolvimento (apenas se Authority estiver configurado)
if (app.Environment.IsDevelopment() && !string.IsNullOrEmpty(jwtAuthority))
{
    try
    {
        Log.Information("Pré-carregando metadados JWT do provedor OIDC...");
        var jwtBearerOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>>();
        var options = jwtBearerOptions.Get(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme);
        
        if (options?.ConfigurationManager != null)
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
            Log.Information("JWT não configurado - Inbound.Api confiando no Gateway para autenticação");
        }
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Não foi possível pré-carregar metadados JWT. O serviço continuará funcionando.");
    }
}
else if (app.Environment.IsDevelopment() && string.IsNullOrEmpty(jwtAuthority))
{
    Log.Information("Inbound.Api rodando sem validação JWT local - confiando no Gateway para autenticação");
}

Log.Information("Inbound.Api starting on {Urls}", app.Urls);
Log.Information("Environment: {Environment}", app.Environment.EnvironmentName);
Log.Information("Swagger available at: http://localhost:5001/swagger");

app.Run();

// Make Program class accessible for WebApplicationFactory
public partial class Program { }
