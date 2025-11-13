using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Serilog;
using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);

// Serilog - Rich logging configuration
var seqUrl = builder.Configuration["Seq:ServerUrl"] ?? "http://localhost:5342";
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .Enrich.WithEnvironmentName()
    .Enrich.WithProperty("Service", "IdentityServer")
    .Enrich.WithProperty("Version", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] [{Service}] {Message:lj}{NewLine}  Properties: {Properties:j}{NewLine}  {Exception}")
    .WriteTo.Seq(seqUrl)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpClient(); // Para o TokenController
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Identity Server API",
        Version = "v1",
        Description = "Servidor OAuth2/OIDC para autenticação e autorização do Hub de Integração"
    });
});

// Authentication - necessário para o IdentityServer
builder.Services.AddAuthentication("Identity.Application")
    .AddCookie("Identity.Application", options =>
    {
        options.Cookie.Name = "IdentityServer.Cookie";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromHours(1);
        options.SlidingExpiration = true;
    });

// IdentityServer
var identityServerBuilder = builder.Services.AddIdentityServer(options =>
{
    options.Events.RaiseErrorEvents = true;
    options.Events.RaiseInformationEvents = true;
    options.Events.RaiseFailureEvents = true;
    options.Events.RaiseSuccessEvents = true;
    
    // Em desenvolvimento, usar HTTP se não estiver configurado
    var issuerUri = builder.Configuration["IdentityServer:IssuerUri"];
    if (string.IsNullOrEmpty(issuerUri))
    {
        // Em desenvolvimento, usar HTTP se o servidor estiver rodando em HTTP
        issuerUri = builder.Environment.IsDevelopment() 
            ? "http://localhost:5002" 
            : "https://localhost:5002";
    }
    options.IssuerUri = issuerUri;
    
    // Configurações de autenticação
    options.Authentication.CookieAuthenticationScheme = "Identity.Application";
    options.Authentication.CookieLifetime = TimeSpan.FromHours(1);
})
    .AddInMemoryClients(Config.Clients)
    .AddInMemoryIdentityResources(Config.IdentityResources)
    .AddInMemoryApiScopes(Config.ApiScopes)
    .AddInMemoryApiResources(Config.ApiResources)
    .AddDeveloperSigningCredential(); // Apenas para desenvolvimento - use certificado em produção

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Health Checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Identity Server API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseSerilogRequestLogging();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseIdentityServer();
app.UseAuthorization();

app.MapControllers();

// Health checks
app.MapHealthChecks("/healthz");
app.MapHealthChecks("/readyz");

// Discovery endpoint (padrão OIDC)
app.MapGet("/.well-known/openid-configuration", () =>
{
    // Em desenvolvimento, usar HTTP; em produção, usar o configurado
    var baseUrl = app.Environment.IsDevelopment() 
        ? builder.Configuration["IdentityServer:IssuerUri"] ?? "http://localhost:5002"
        : builder.Configuration["IdentityServer:IssuerUri"] ?? "https://localhost:5002";
    
    return Results.Ok(new
    {
        issuer = baseUrl,
        authorization_endpoint = $"{baseUrl}/connect/authorize",
        token_endpoint = $"{baseUrl}/connect/token",
        userinfo_endpoint = $"{baseUrl}/connect/userinfo",
        end_session_endpoint = $"{baseUrl}/connect/endsession",
        jwks_uri = $"{baseUrl}/.well-known/openid-configuration/jwks",
        grant_types_supported = new[] { "authorization_code", "client_credentials", "refresh_token" },
        response_types_supported = new[] { "code", "token", "id_token", "token id_token" },
        scopes_supported = new[] { "openid", "profile", "hub.api.write", "hub.api.read" },
        claims_supported = new[] { "sub", "name", "email", "scope" }
    });
});

Log.Information("IdentityServer starting on {Urls}", app.Urls);
var issuerUri = builder.Configuration["IdentityServer:IssuerUri"] ?? "http://localhost:5002";
Log.Information("Discovery endpoint: {IssuerUri}/.well-known/openid-configuration", issuerUri);
Log.Information("Swagger: {IssuerUri}/swagger", issuerUri);

app.Run();

// Configuração do IdentityServer
public static class Config
{
    // Scopes da API
    public static IEnumerable<ApiScope> ApiScopes =>
        new List<ApiScope>
        {
            new ApiScope("hub.api.write", "Write access to Hub API"),
            new ApiScope("hub.api.read", "Read access to Hub API")
        };

    // Recursos da API
    public static IEnumerable<ApiResource> ApiResources =>
        new List<ApiResource>
        {
            new ApiResource("hub-api", "Hub Integration API")
            {
                Scopes = { "hub.api.write", "hub.api.read" },
                UserClaims = { "sub", "name", "email", "scope" }
            }
        };

    // Recursos de Identidade
    public static IEnumerable<IdentityResource> IdentityResources =>
        new List<IdentityResource>
        {
            new IdentityResources.OpenId(),
            new IdentityResources.Profile(),
            new IdentityResources.Email()
        };

    // Clientes OAuth2
    public static IEnumerable<Client> Clients =>
        new List<Client>
        {
            // Cliente para aplicações (Client Credentials)
            new Client
            {
                ClientId = "hub-client",
                ClientName = "Hub Integration Client",
                AllowedGrantTypes = GrantTypes.ClientCredentials,
                ClientSecrets = { new Secret("hub-secret".Sha256()) },
                AllowedScopes = { "hub.api.write", "hub.api.read" },
                AccessTokenLifetime = 3600, // 1 hora
                Claims = new List<ClientClaim>
                {
                    new ClientClaim("partner_code", "PARTNER01")
                }
            },
            // Cliente para desenvolvimento/teste (Authorization Code)
            new Client
            {
                ClientId = "hub-dev-client",
                ClientName = "Hub Development Client",
                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = false,
                RequireClientSecret = false, // Apenas para desenvolvimento
                RedirectUris = { "https://localhost:5000/swagger/oauth2-redirect.html", "http://localhost:5000/swagger/oauth2-redirect.html" },
                PostLogoutRedirectUris = { "https://localhost:5000/swagger", "http://localhost:5000/swagger" },
                AllowedScopes = { "openid", "profile", "hub.api.write", "hub.api.read" },
                AccessTokenLifetime = 3600,
                AllowOfflineAccess = true
            }
        };
}

