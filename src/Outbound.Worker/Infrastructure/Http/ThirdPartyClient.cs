using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Polly;
using Shared.Contracts;
using Shared.Policies;

namespace Outbound.Worker.Infrastructure.Http;

public interface IThirdPartyClient
{
    Task<ThirdPartyResponse> SendRequestAsync(DispatchToPartner command, CancellationToken cancellationToken = default);
}

public class ThirdPartyClient : IThirdPartyClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ThirdPartyClient> _logger;

    public ThirdPartyClient(
        IHttpClientFactory httpClientFactory,
        ILogger<ThirdPartyClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("ThirdParty");
        _logger = logger;
    }

    public async Task<ThirdPartyResponse> SendRequestAsync(DispatchToPartner command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Sending request to {Endpoint} for correlation ID: {CorrelationId}",
            command.Endpoint, command.CorrelationId);

        // Mock response for localhost endpoints in development
        if (command.Endpoint.Host == "localhost" || command.Endpoint.Host == "127.0.0.1")
        {
            _logger.LogInformation("Using mock response for localhost endpoint: {Endpoint}", command.Endpoint);
            
            // Simulate a successful response
            await Task.Delay(100, cancellationToken); // Simulate network delay
            
            var mockResponse = new Dictionary<string, object>
            {
                ["status"] = "success",
                ["correlationId"] = command.CorrelationId.ToString(),
                ["partnerCode"] = command.PartnerCode,
                ["message"] = "Request processed successfully (mock)"
            };
            
            return new ThirdPartyResponse
            {
                Success = true,
                StatusCode = 200,
                Response = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(mockResponse))
            };
        }

        var request = new HttpRequestMessage(HttpMethod.Post, command.Endpoint)
        {
            Content = JsonContent.Create(command.Payload)
        };

        request.Headers.Add("X-Correlation-Id", command.CorrelationId.ToString());

        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var responseJson = string.IsNullOrEmpty(responseContent)
                ? (JsonElement?)null
                : JsonSerializer.Deserialize<JsonElement>(responseContent);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Request succeeded for correlation ID: {CorrelationId}, Status: {StatusCode}",
                    command.CorrelationId, (int)response.StatusCode);

                return new ThirdPartyResponse
                {
                    Success = true,
                    StatusCode = (int)response.StatusCode,
                    Response = responseJson
                };
            }
            else
            {
                _logger.LogWarning("Request failed for correlation ID: {CorrelationId}, Status: {StatusCode}",
                    command.CorrelationId, (int)response.StatusCode);

                return new ThirdPartyResponse
                {
                    Success = false,
                    StatusCode = (int)response.StatusCode,
                    Response = responseJson
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception sending request for correlation ID: {CorrelationId}",
                command.CorrelationId);

            throw;
        }
    }
}

public class ThirdPartyResponse
{
    public bool Success { get; set; }
    public int StatusCode { get; set; }
    public JsonElement? Response { get; set; }
}

