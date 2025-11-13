using Microsoft.AspNetCore.RateLimiting;
using Serilog;
using Shared.Observability;
using Shared.Security;
using Shared.Web;
using System.Threading.RateLimiting;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Serilog
var seqUrl = builder.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341";
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .Enrich.WithEnvironmentName()
    .Enrich.WithProperty("Service", "Gateway.Yarp")
    .Enrich.WithProperty("Version", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] [{Service}] {Message:lj}{NewLine}  Properties: {Properties:j}{NewLine}  {Exception}")
    .WriteTo.Seq(seqUrl)
    .CreateLogger();

builder.Host.UseSerilog();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Hub Integration Gateway API",
        Version = "v1",
        Description = "API Gateway usando YARP para rotear requisições aos serviços backend. " +
                     "Os endpoints abaixo são automaticamente roteados para a Inbound API (http://localhost:5001)."
    });
    
    // Adicionar schema para CreateRequest
    c.MapType<object>(() => new Microsoft.OpenApi.Models.OpenApiSchema
    {
        Type = "object",
        Properties = new Dictionary<string, Microsoft.OpenApi.Models.OpenApiSchema>
        {
            ["partnerCode"] = new Microsoft.OpenApi.Models.OpenApiSchema 
            { 
                Type = "string", 
                Example = new Microsoft.OpenApi.Any.OpenApiString("PARTNER01"),
                Description = "Código do parceiro"
            },
            ["type"] = new Microsoft.OpenApi.Models.OpenApiSchema 
            { 
                Type = "string", 
                Example = new Microsoft.OpenApi.Any.OpenApiString("ORDER"),
                Description = "Tipo da requisição"
            },
            ["payload"] = new Microsoft.OpenApi.Models.OpenApiSchema 
            { 
                Type = "string", 
                Example = new Microsoft.OpenApi.Any.OpenApiString("{\"orderId\":\"12345\",\"customerId\":\"CUST001\"}"),
                Description = "Payload da requisição (JSON string)"
            }
        },
        Required = new HashSet<string> { "partnerCode", "type", "payload" }
    });
});
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        // Extract partner code from JWT claim or header
        var partnerCode = context.User.FindFirst("partner_code")?.Value
            ?? context.Request.Headers["X-Partner-Code"].ToString()
            ?? "default";

        return RateLimitPartition.GetTokenBucketLimiter(
            partitionKey: partnerCode,
            factory: _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 100,
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                TokensPerPeriod = 10,
                AutoReplenishment = true
            });
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        context.HttpContext.Response.Headers["Retry-After"] = "1";
        await context.HttpContext.Response.WriteAsync("Rate limit exceeded", cancellationToken);
    };
});

// Authentication & Authorization
// Em desenvolvimento, permitir validação flexível se não houver servidor OAuth2
var allowDevWithoutAuth = builder.Configuration.GetValue<bool>("Jwt:AllowDevelopmentWithoutAuthority", defaultValue: true);
builder.Services.AddJwtAuthentication(
    authority: builder.Configuration["Jwt:Authority"] ?? "https://localhost:5001",
    audience: builder.Configuration["Jwt:Audience"] ?? "hub-api",
    allowDevelopmentWithoutAuthority: allowDevWithoutAuth);

// OpenTelemetry
builder.Services.AddOpenTelemetry(
    serviceName: "gateway-yarp",
    otlpEndpoint: builder.Configuration["OpenTelemetry:OtlpEndpoint"]);

// Health Checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Middleware
app.UseSerilogRequestLogging();
app.UseCorrelationMiddleware();
app.UseGlobalErrorHandling();

// Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        // Swagger do Gateway (info sobre rotas)
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Gateway API v1");
        
        // Swagger da Inbound API (proxy)
        c.SwaggerEndpoint("/swagger-inbound/v1/swagger.json", "Inbound API v1 (via Gateway)");
        
        c.RoutePrefix = "swagger";
    });
}

// Health (antes do YARP para não ser interceptado)
app.MapHealthChecks("/healthz");
app.MapHealthChecks("/readyz");

// Gateway Routes Info (para Swagger)
app.MapGet("/", () => Results.Redirect("/swagger"))
    .ExcludeFromDescription();

app.MapGet("/api/info", () => new
{
    Service = "Gateway.Yarp",
    Version = "1.0",
    Routes = new[]
    {
        new { Path = "/api/requests/{**catch-all}", Destination = "http://localhost:5001/requests", Description = "Roteia requisições para Inbound API" }
    }
})
.WithTags("Gateway")
.WithDescription("Informações sobre as rotas do Gateway");

// YARP Reverse Proxy (deve vir ANTES dos endpoints para interceptar)
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.Use((context, next) =>
    {
        // Forward correlation ID
        var correlationId = context.Items["CorrelationId"]?.ToString();
        if (!string.IsNullOrEmpty(correlationId))
        {
            context.Request.Headers["X-Correlation-Id"] = correlationId;
        }

        return next();
    });
});

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

// NOTA: Os endpoints /api/requests são roteados automaticamente pelo YARP.
// Para ver e testar os endpoints, use o Swagger da Inbound API via Gateway:
// http://localhost:5187/swagger-inbound

Log.Information("Gateway.Yarp starting on {Urls}", app.Urls);
Log.Information("Gateway Swagger: http://localhost:5187/swagger");
Log.Information("Inbound API Swagger (via Gateway): http://localhost:5187/swagger-inbound");
Log.Information("Inbound API Swagger (direto): http://localhost:5001/swagger");

app.Run();
