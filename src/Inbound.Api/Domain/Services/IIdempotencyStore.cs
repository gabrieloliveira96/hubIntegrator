namespace Inbound.Api.Domain.Services;

public interface IIdempotencyStore
{
    Task<Guid?> GetExistingCorrelationIdAsync(string idempotencyKey, CancellationToken cancellationToken = default);
    Task<Guid> CreateDedupKeyAsync(string idempotencyKey, Guid correlationId, CancellationToken cancellationToken = default);
}

