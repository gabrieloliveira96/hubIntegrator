using Inbound.Api.Domain;
using Inbound.Api.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Inbound.Api.Infrastructure.Persistence.Repositories;

public class RequestRepository : IRequestRepository
{
    private readonly InboxDbContext _dbContext;
    private readonly ILogger<RequestRepository> _logger;

    public RequestRepository(
        InboxDbContext dbContext,
        ILogger<RequestRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Request?> GetByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Requests
            .FirstOrDefaultAsync(r => r.CorrelationId == correlationId.ToString(), cancellationToken);
    }

    public async Task<Request?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Requests
            .FirstOrDefaultAsync(r => r.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public async Task<Request> CreateAsync(Request request, CancellationToken cancellationToken = default)
    {
        _dbContext.Requests.Add(request);
        await _dbContext.SaveChangesAsync(cancellationToken);
        
        _logger.LogDebug("Request created. CorrelationId: {CorrelationId}", request.CorrelationId);
        
        return request;
    }

    public async Task UpdateAsync(Request request, CancellationToken cancellationToken = default)
    {
        _dbContext.Requests.Update(request);
        await _dbContext.SaveChangesAsync(cancellationToken);
        
        _logger.LogDebug("Request updated. CorrelationId: {CorrelationId}", request.CorrelationId);
    }

    public async Task<bool> ExistsByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Requests
            .AnyAsync(r => r.CorrelationId == correlationId.ToString(), cancellationToken);
    }
}
