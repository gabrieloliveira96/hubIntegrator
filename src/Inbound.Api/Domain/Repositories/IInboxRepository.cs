using Shared.Persistence;

namespace Inbound.Api.Domain.Repositories;

public interface IInboxRepository
{
    Task<InboxMessage> AddAsync(InboxMessage message, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

