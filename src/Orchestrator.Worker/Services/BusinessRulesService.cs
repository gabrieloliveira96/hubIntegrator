namespace Orchestrator.Worker.Services;

public interface IBusinessRulesService
{
    Task<bool> ValidateRequestAsync(string partnerCode, string type, CancellationToken cancellationToken = default);
    Task EnrichRequestDataAsync(Guid correlationId, string partnerCode, CancellationToken cancellationToken = default);
}

public class BusinessRulesService : IBusinessRulesService
{
    private readonly ILogger<BusinessRulesService> _logger;

    public BusinessRulesService(ILogger<BusinessRulesService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> ValidateRequestAsync(string partnerCode, string type, CancellationToken cancellationToken = default)
    {
        // Simulate validation logic
        await Task.Delay(100, cancellationToken);

        _logger.LogInformation("Validated request for partner: {PartnerCode}, type: {Type}", partnerCode, type);

        return true;
    }

    public async Task EnrichRequestDataAsync(Guid correlationId, string partnerCode, CancellationToken cancellationToken = default)
    {
        // Simulate data enrichment (e.g., fetch from cache, external service)
        await Task.Delay(50, cancellationToken);

        _logger.LogInformation("Enriched data for correlation ID: {CorrelationId}, partner: {PartnerCode}",
            correlationId, partnerCode);
    }
}

