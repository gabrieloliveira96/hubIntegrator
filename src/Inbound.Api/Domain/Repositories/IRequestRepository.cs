namespace Inbound.Api.Domain.Repositories;

public interface IRequestRepository
{
    Task<Request?> GetByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken = default);
    Task<Request?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);
    Task<Request> CreateAsync(Request request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Request request, CancellationToken cancellationToken = default);
    Task<bool> ExistsByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken = default);
}

