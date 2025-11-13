using Inbound.Api.Domain;
using Inbound.Api.Domain.Repositories;
using Inbound.Api.Domain.Services;
using StackExchange.Redis;

namespace Inbound.Api.Infrastructure.Persistence;

public class IdempotencyStore : IIdempotencyStore
{
    private readonly IDedupKeyRepository _dedupKeyRepository;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<IdempotencyStore> _logger;

    public IdempotencyStore(
        IDedupKeyRepository dedupKeyRepository,
        IConnectionMultiplexer redis,
        ILogger<IdempotencyStore> logger)
    {
        _dedupKeyRepository = dedupKeyRepository;
        _redis = redis;
        _logger = logger;
    }

    public async Task<Guid?> GetExistingCorrelationIdAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var lockKey = $"idempotency:lock:{idempotencyKey}";
        var lockValue = Guid.NewGuid().ToString();

        // Try to acquire distributed lock (5 second timeout)
        var lockAcquired = await db.LockTakeAsync(lockKey, lockValue, TimeSpan.FromSeconds(5));

        if (!lockAcquired)
        {
            _logger.LogWarning("Could not acquire lock for idempotency key: {Key}", idempotencyKey);
            return null;
        }

        try
        {
            var dedupKey = await _dedupKeyRepository.GetByKeyAsync(idempotencyKey, cancellationToken);

            if (dedupKey != null)
            {
                _logger.LogInformation("Found existing correlation ID for idempotency key: {Key} -> {CorrelationId}",
                    idempotencyKey, dedupKey.CorrelationId);
                return Guid.Parse(dedupKey.CorrelationId);
            }

            return null;
        }
        finally
        {
            await db.LockReleaseAsync(lockKey, lockValue);
        }
    }

    public async Task<Guid> CreateDedupKeyAsync(
        string idempotencyKey,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        // Cria o DedupKey para idempotência usando o repositório
        var dedupKey = new DedupKey
        {
            Id = Guid.NewGuid(),
            Key = idempotencyKey,
            CorrelationId = correlationId.ToString(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _dedupKeyRepository.CreateAsync(dedupKey, cancellationToken);

        _logger.LogInformation("Created dedup key for correlation ID: {CorrelationId} with idempotency key: {Key}",
            correlationId, idempotencyKey);

        return correlationId;
    }
}

