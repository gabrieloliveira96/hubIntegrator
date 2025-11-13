using Inbound.Api.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Shared.Persistence;

namespace Inbound.Api.Infrastructure.Persistence.Repositories;

public class InboxRepository : IInboxRepository
{
    private readonly InboxDbContext _dbContext;
    private readonly ILogger<InboxRepository> _logger;

    public InboxRepository(
        InboxDbContext dbContext,
        ILogger<InboxRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<InboxMessage> AddAsync(InboxMessage message, CancellationToken cancellationToken = default)
    {
        _dbContext.Inbox.Add(message);
        await _dbContext.SaveChangesAsync(cancellationToken);
        
        _logger.LogDebug("Inbox message added. MessageId: {MessageId}, CorrelationId: {CorrelationId}", 
            message.MessageId, message.CorrelationId);
        
        return message;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogDebug("Inbox changes saved");
    }
}
