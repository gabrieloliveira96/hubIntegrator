namespace Inbound.Api.Domain.Services;

public interface IMqPublisher
{
    Task PublishRequestReceivedAsync(Domain.Request request, CancellationToken cancellationToken = default);
}

