using Inbound.Api.Domain;
using Inbound.Api.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Inbound.Api.Infrastructure.Persistence.Repositories;

public class DedupKeyRepository : IDedupKeyRepository
{
    private readonly InboxDbContext _dbContext;
    private readonly ILogger<DedupKeyRepository> _logger;

    public DedupKeyRepository(
        InboxDbContext dbContext,
        ILogger<DedupKeyRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<DedupKey?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        return await _dbContext.DedupKeys
            .FirstOrDefaultAsync(d => d.Key == key, cancellationToken);
    }

    public async Task<DedupKey> CreateAsync(DedupKey dedupKey, CancellationToken cancellationToken = default)
    {
        _dbContext.DedupKeys.Add(dedupKey);
        await _dbContext.SaveChangesAsync(cancellationToken);
        
        _logger.LogDebug("DedupKey created. Key: {Key}, CorrelationId: {CorrelationId}", 
            dedupKey.Key, dedupKey.CorrelationId);
        
        return dedupKey;
    }

    public async Task<bool> ExistsByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        return await _dbContext.DedupKeys
            .AnyAsync(d => d.Key == key, cancellationToken);
    }
}

