using Inbound.Api.Domain;
using MassTransit;
using Shared.Contracts;
using System.Text.Json;

namespace Inbound.Api.Infrastructure.Messaging;

public interface IMqPublisher
{
    Task PublishRequestReceivedAsync(Request request, CancellationToken cancellationToken = default);
}

public class MqPublisher : IMqPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<MqPublisher> _logger;

    public MqPublisher(IPublishEndpoint publishEndpoint, ILogger<MqPublisher> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task PublishRequestReceivedAsync(Request request, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Deserialize<JsonElement>(request.Payload);

        var message = new RequestReceived(
            CorrelationId: Guid.Parse(request.CorrelationId),
            PartnerCode: request.PartnerCode,
            Type: request.Type,
            Payload: payload,
            CreatedAt: request.CreatedAt
        );

        await _publishEndpoint.Publish(message, cancellationToken);

        _logger.LogInformation("Published RequestReceived for correlation ID: {CorrelationId}",
            request.CorrelationId);
    }
}

