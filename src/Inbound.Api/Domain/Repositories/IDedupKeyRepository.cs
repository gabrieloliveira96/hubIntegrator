namespace Inbound.Api.Domain.Repositories;

public interface IDedupKeyRepository
{
    Task<DedupKey?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
    Task<DedupKey> CreateAsync(DedupKey dedupKey, CancellationToken cancellationToken = default);
    Task<bool> ExistsByKeyAsync(string key, CancellationToken cancellationToken = default);
}

