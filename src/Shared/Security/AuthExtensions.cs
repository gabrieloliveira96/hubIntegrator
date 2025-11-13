using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

namespace Shared.Security;

public static class AuthExtensions
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        string authority,
        string audience,
        bool allowDevelopmentWithoutAuthority = false)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.Audience = audience;
                
                // Em desenvolvimento, permitir validação mais flexível se necessário
                // Verifica o ambiente através da variável de ambiente
                var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development" ||
                                   Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == "Development";
                
                // Em desenvolvimento, configurar para aceitar HTTP e HTTPS
                if (isDevelopment)
                {
                    // Normalizar authority para HTTP se necessário
                    var httpAuthority = authority.Replace("https://", "http://");
                    // Forçar o uso de HTTP para metadados em desenvolvimento
                    options.Authority = httpAuthority;
                    options.RequireHttpsMetadata = false;
                    options.MetadataAddress = httpAuthority + "/.well-known/openid-configuration";
                    
                    // Configurar BackchannelHttpHandler para ignorar validação SSL
                    var httpClientHandler = new System.Net.Http.HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                    };
                    options.BackchannelHttpHandler = httpClientHandler;
                    
                    // Criar ConfigurationManager customizado que aceita HTTP
                    var httpClient = new System.Net.Http.HttpClient(httpClientHandler);
                    var documentRetriever = new Microsoft.IdentityModel.Protocols.HttpDocumentRetriever(httpClient)
                    {
                        RequireHttps = false
                    };
                    
                    options.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                        options.MetadataAddress,
                        new OpenIdConnectConfigurationRetriever(),
                        documentRetriever)
                    {
                        AutomaticRefreshInterval = TimeSpan.FromHours(1),
                        RefreshInterval = TimeSpan.FromMinutes(30)
                    };
                    
                    // Configurar eventos para debug
                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = async context =>
                        {
                            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
                            var token = context.Token;
                            if (!string.IsNullOrEmpty(token))
                            {
                                logger.LogDebug("Token JWT recebido (primeiros 50 chars): {TokenPrefix}...", 
                                    token.Length > 50 ? token.Substring(0, 50) : token);
                            }
                            
                            // Garantir que os metadados estão carregados
                            try
                            {
                                if (options.ConfigurationManager != null)
                                {
                                    var config = await options.ConfigurationManager.GetConfigurationAsync(CancellationToken.None);
                                    logger.LogDebug("Metadados carregados. Issuer: {Issuer}, JWKS Keys: {KeyCount}", 
                                        config.Issuer, config.SigningKeys?.Count ?? 0);
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.LogWarning(ex, "Erro ao carregar metadados do provedor OIDC");
                            }
                        },
                        OnAuthenticationFailed = context =>
                        {
                            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
                            logger.LogError(context.Exception, "Falha na autenticação JWT: {Message}", context.Exception.Message);
                            if (context.Exception.InnerException != null)
                            {
                                logger.LogError(context.Exception.InnerException, "Inner exception: {Message}", context.Exception.InnerException.Message);
                            }
                            logger.LogError("MetadataAddress: {MetadataAddress}, Authority: {Authority}", 
                                options.MetadataAddress, options.Authority);
                            return Task.CompletedTask;
                        },
                        OnChallenge = context =>
                        {
                            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
                            logger.LogWarning("Challenge JWT: {Error}, {ErrorDescription}", 
                                context.Error, context.ErrorDescription);
                            logger.LogWarning("MetadataAddress: {MetadataAddress}, Authority: {Authority}", 
                                options.MetadataAddress, options.Authority);
                            
                            // IMPORTANTE: Chamar HandleResponse() para indicar que já lidamos com a resposta
                            // Isso impede que o framework retorne 401 automaticamente
                            // O middleware customizado do Gateway vai escrever a resposta JSON detalhada
                            context.HandleResponse();
                            return Task.CompletedTask;
                        },
                        OnTokenValidated = context =>
                        {
                            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
                            logger.LogDebug("Token JWT validado com sucesso para: {Subject}", 
                                context.Principal?.Identity?.Name ?? "unknown");
                            return Task.CompletedTask;
                        }
                    };
                    
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = !allowDevelopmentWithoutAuthority,
                        ValidateAudience = !allowDevelopmentWithoutAuthority,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = !allowDevelopmentWithoutAuthority,
                        RequireSignedTokens = !allowDevelopmentWithoutAuthority,
                        ClockSkew = TimeSpan.FromMinutes(5),
                        // Aceitar issuer com HTTP ou HTTPS em desenvolvimento
                        ValidIssuers = new[] { authority, httpAuthority }
                    };
                }
                else
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        RequireSignedTokens = true,
                        ClockSkew = TimeSpan.FromMinutes(5),
                        ValidIssuer = authority
                    };
                }
                
                // Em desenvolvimento sem Authority, permitir qualquer token
                if (isDevelopment && allowDevelopmentWithoutAuthority)
                {
                    options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(
                        System.Text.Encoding.UTF8.GetBytes("development-key-not-for-production"));
                    options.TokenValidationParameters.ValidateIssuerSigningKey = false;
                }
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("Write", policy =>
                policy.RequireClaim("scope", "hub.api.write"));
            options.AddPolicy("Read", policy =>
                policy.RequireClaim("scope", "hub.api.read"));
        });

        return services;
    }

    public static IApplicationBuilder UseAntiReplay(this IApplicationBuilder app)
    {
        return app.UseMiddleware<AntiReplayMiddleware>();
    }
}

public class AntiReplayMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AntiReplayMiddleware> _logger;
    private const int TimestampToleranceSeconds = 300; // 5 minutes

    public AntiReplayMiddleware(RequestDelegate next, ILogger<AntiReplayMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IConnectionMultiplexer redis, IWebHostEnvironment env)
    {
        // Skip anti-replay for health checks and Swagger in development
        var path = context.Request.Path.Value?.ToLower() ?? "";
        var isHealthCheck = path == "/healthz" || path == "/readyz";
        var isSwagger = path.StartsWith("/swagger") || path == "/" || path == "";
        var isSwaggerJson = path.Contains("/swagger.json");
        
        // Skip anti-replay for GET requests (read-only operations don't need idempotency/anti-replay)
        var isGetRequest = context.Request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase);
        
        if (env.EnvironmentName == "Development" && (isHealthCheck || isSwagger || isSwaggerJson))
        {
            await _next(context);
            return;
        }
        
        // GET requests don't need anti-replay protection
        if (isGetRequest)
        {
            await _next(context);
            return;
        }

        var nonce = context.Request.Headers["X-Nonce"].ToString();
        var timestampHeader = context.Request.Headers["X-Timestamp"].ToString();

        // Generate example values for error responses
        var exampleIdempotencyKey = Guid.NewGuid().ToString();
        var exampleNonce = Guid.NewGuid().ToString();
        var exampleTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        if (string.IsNullOrEmpty(nonce) || string.IsNullOrEmpty(timestampHeader))
        {
            var missingHeaders = new List<string>();
            if (string.IsNullOrEmpty(nonce)) missingHeaders.Add("X-Nonce");
            if (string.IsNullOrEmpty(timestampHeader)) missingHeaders.Add("X-Timestamp");

            context.Response.StatusCode = 400;
            context.Response.ContentType = "application/json";
            
            var errorResponse = JsonSerializer.Serialize(new
            {
                error = "Missing required headers",
                message = $"The following headers are required: {string.Join(", ", missingHeaders)}",
                missing = missingHeaders,
                example = new
                {
                    IdempotencyKey = exampleIdempotencyKey,
                    XNonce = exampleNonce,
                    XTimestamp = exampleTimestamp
                },
                howToFix = "Add these headers to your request. In Swagger UI, click the 'Authorize' button and add them as custom headers."
            });
            
            await context.Response.WriteAsync(errorResponse);
            return;
        }

        if (!long.TryParse(timestampHeader, out var timestamp))
        {
            context.Response.StatusCode = 400;
            context.Response.ContentType = "application/json";
            
            var errorResponse = JsonSerializer.Serialize(new
            {
                error = "Invalid X-Timestamp format",
                message = "X-Timestamp must be a Unix timestamp in seconds (integer)",
                received = timestampHeader,
                example = new
                {
                    XTimestamp = exampleTimestamp,
                    currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                },
                howToFix = $"Use a Unix timestamp in seconds. Current timestamp: {exampleTimestamp}"
            });
            
            await context.Response.WriteAsync(errorResponse);
            return;
        }

        var requestTime = DateTimeOffset.FromUnixTimeSeconds(timestamp);
        var now = DateTimeOffset.UtcNow;
        var timeDiff = Math.Abs((now - requestTime).TotalSeconds);

        if (timeDiff > TimestampToleranceSeconds)
        {
            _logger.LogWarning("Request timestamp out of tolerance. Diff: {TimeDiff}s", timeDiff);
            context.Response.StatusCode = 400;
            context.Response.ContentType = "application/json";
            
            var errorResponse = JsonSerializer.Serialize(new
            {
                error = "Request timestamp out of tolerance",
                message = $"The request timestamp is more than {TimestampToleranceSeconds} seconds ({timeDiff:F0}s) away from the server time",
                receivedTimestamp = timestamp,
                receivedTime = requestTime.ToString("O"),
                serverTime = now.ToString("O"),
                differenceSeconds = timeDiff,
                toleranceSeconds = TimestampToleranceSeconds,
                example = new
                {
                    XTimestamp = exampleTimestamp
                },
                howToFix = $"Use a current timestamp. Current server timestamp: {exampleTimestamp}"
            });
            
            await context.Response.WriteAsync(errorResponse);
            return;
        }

        var db = redis.GetDatabase();
        var nonceKey = $"nonce:{nonce}";
        var exists = await db.StringSetAsync(nonceKey, "1", TimeSpan.FromMinutes(5), When.NotExists);

        if (!exists)
        {
            _logger.LogWarning("Duplicate nonce detected: {Nonce}", nonce);
            context.Response.StatusCode = 409;
            context.Response.ContentType = "application/json";
            
            var errorResponse = JsonSerializer.Serialize(new
            {
                error = "Duplicate request",
                message = "This nonce has already been used. Each request must have a unique nonce.",
                nonce = nonce,
                example = new
                {
                    XNonce = exampleNonce
                },
                howToFix = "Generate a new unique GUID for the X-Nonce header for each request."
            });
            
            await context.Response.WriteAsync(errorResponse);
            return;
        }

        await _next(context);
    }
}

