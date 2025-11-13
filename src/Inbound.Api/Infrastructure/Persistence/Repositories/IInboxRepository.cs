using Shared.Persistence;

namespace Inbound.Api.Infrastructure.Persistence.Repositories;

public interface IInboxRepository
{
    Task<InboxMessage> AddAsync(InboxMessage message, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
