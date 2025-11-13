using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;

namespace IdentityServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TokenController : ControllerBase
{
    private readonly ILogger<TokenController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public TokenController(
        ILogger<TokenController> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    /// <summary>
    /// Obter token JWT de forma simplificada (JSON)
    /// </summary>
    [HttpPost("obter")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ObterToken([FromBody] TokenRequest request)
    {
        try
        {
            // Validações básicas
            if (string.IsNullOrEmpty(request.ClientId))
            {
                return BadRequest(new { error = "ClientId é obrigatório" });
            }

            // Preparar o body para form-urlencoded (formato esperado pelo IdentityServer)
            var formData = new List<KeyValuePair<string, string>>
            {
                new("grant_type", "client_credentials"),
                new("client_id", request.ClientId)
            };

            if (!string.IsNullOrEmpty(request.ClientSecret))
            {
                formData.Add(new KeyValuePair<string, string>("client_secret", request.ClientSecret));
            }

            var scope = request.Scopes != null && request.Scopes.Length > 0
                ? string.Join(" ", request.Scopes)
                : "hub.api.write hub.api.read";

            formData.Add(new KeyValuePair<string, string>("scope", scope));

            // Chamar o endpoint /connect/token do IdentityServer
            var httpClient = _httpClientFactory.CreateClient();
            
            // Detectar se está rodando em Docker (porta interna 8080) ou localmente (porta 5002)
            var isDocker = Environment.GetEnvironmentVariable("ASPNETCORE_URLS")?.Contains("8080") == true;
            var baseUrl = isDocker 
                ? "http://localhost:8080"  // Porta interna do container
                : (_configuration["IdentityServer:IssuerUri"] ?? "http://localhost:5002");  // Porta externa/local
            
            var tokenEndpoint = $"{baseUrl}/connect/token";

            var formContent = new FormUrlEncodedContent(formData);
            formContent.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

            var response = await httpClient.PostAsync(tokenEndpoint, formContent);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Erro ao obter token: {StatusCode} - {Error}", response.StatusCode, errorContent);
                return StatusCode((int)response.StatusCode, new { error = "Erro ao obter token", details = errorContent });
            }

            var tokenJson = await response.Content.ReadAsStringAsync();
            var tokenResult = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(tokenJson);

            if (tokenResult.ValueKind == System.Text.Json.JsonValueKind.Null || tokenResult.ValueKind == System.Text.Json.JsonValueKind.Undefined)
            {
                return BadRequest(new { error = "Resposta inválida do IdentityServer" });
            }

            // Retornar no formato simplificado
            return Ok(new TokenResponse
            {
                AccessToken = tokenResult.TryGetProperty("access_token", out var accessToken) ? accessToken.GetString() ?? string.Empty : string.Empty,
                TokenType = tokenResult.TryGetProperty("token_type", out var tokenType) ? tokenType.GetString() ?? "Bearer" : "Bearer",
                ExpiresIn = tokenResult.TryGetProperty("expires_in", out var expiresIn) ? expiresIn.GetInt32() : 3600,
                Scope = tokenResult.TryGetProperty("scope", out var scopeProp) ? scopeProp.GetString() ?? scope : scope
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar requisição de token");
            return StatusCode(500, new { error = "Erro interno ao obter token", message = ex.Message });
        }
    }

    /// <summary>
    /// Informações sobre como obter tokens JWT
    /// </summary>
    [HttpGet("info")]
    [ProducesResponseType(typeof(TokenInfoResponse), StatusCodes.Status200OK)]
    public IActionResult GetTokenInfo()
    {
        return Ok(new TokenInfoResponse
        {
            Message = "Use o endpoint /api/token/obter para obter tokens de forma simplificada (JSON)",
            Endpoints = new
            {
                TokenEndpoint = "/connect/token",
                SimplifiedEndpoint = "/api/token/obter",
                DiscoveryEndpoint = "/.well-known/openid-configuration",
                AuthorizationEndpoint = "/connect/authorize"
            },
            Clients = new[]
            {
                new
                {
                    ClientId = "hub-client",
                    GrantType = "client_credentials",
                    ClientSecret = "hub-secret",
                    Scopes = new[] { "hub.api.write", "hub.api.read" },
                    Example = new
                    {
                        Method = "POST",
                        Url = "/api/token/obter",
                        Headers = new { ContentType = "application/json" },
                        Body = new
                        {
                            clientId = "hub-client",
                            clientSecret = "hub-secret",
                            scopes = new[] { "hub.api.write", "hub.api.read" }
                        }
                    }
                }
            }
        });
    }
}

public class TokenInfoResponse
{
    public string Message { get; set; } = string.Empty;
    public object Endpoints { get; set; } = new();
    public object[] Clients { get; set; } = Array.Empty<object>();
}

public class TokenRequest
{
    /// <summary>
    /// Client ID (obrigatório). Exemplo: "hub-client"
    /// </summary>
    public string ClientId { get; set; } = "hub-client";
    
    /// <summary>
    /// Client Secret. Exemplo: "hub-secret"
    /// </summary>
    public string? ClientSecret { get; set; } = "hub-secret";
    
    /// <summary>
    /// Scopes solicitados. Padrão: ["hub.api.write", "hub.api.read"]
    /// </summary>
    public string[]? Scopes { get; set; } = new[] { "hub.api.write", "hub.api.read" };
}

public class TokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
    public string Scope { get; set; } = string.Empty;
}

