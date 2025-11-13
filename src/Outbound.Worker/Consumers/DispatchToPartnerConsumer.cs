using MassTransit;
using Outbound.Worker.Infrastructure.Http;
using Outbound.Worker.Infrastructure.Persistence;
using Shared.Contracts;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Outbound.Worker.Consumers;

public class DispatchToPartnerConsumer : IConsumer<DispatchToPartner>
{
    private readonly IThirdPartyClient _thirdPartyClient;
    private readonly OutboxDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<DispatchToPartnerConsumer> _logger;

    public DispatchToPartnerConsumer(
        IThirdPartyClient thirdPartyClient,
        OutboxDbContext dbContext,
        IPublishEndpoint publishEndpoint,
        ILogger<DispatchToPartnerConsumer> logger)
    {
        _thirdPartyClient = thirdPartyClient;
        _dbContext = dbContext;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DispatchToPartner> context)
    {
        var command = context.Message;
        _logger.LogInformation("Consuming DispatchToPartner for correlation ID: {CorrelationId}",
            command.CorrelationId);

        try
        {
            var response = await _thirdPartyClient.SendRequestAsync(command, context.CancellationToken);

            if (response.Success)
            {
                // Persist to Outbox
                var completedEvent = new RequestCompleted(
                    command.CorrelationId,
                    command.PartnerCode,
                    response.StatusCode,
                    "Completed",
                    response.Response);

                var outboxMessage = new Shared.Persistence.OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    MessageType = nameof(RequestCompleted),
                    Payload = JsonSerializer.Serialize(completedEvent),
                    CreatedAt = DateTimeOffset.UtcNow,
                    CorrelationId = command.CorrelationId.ToString()
                };

                _dbContext.Outbox.Add(outboxMessage);
                await _dbContext.SaveChangesAsync(context.CancellationToken);

                // Publish immediately (Outbox pattern ensures at-least-once)
                await _publishEndpoint.Publish(completedEvent, context.CancellationToken);

                _logger.LogInformation("Request completed successfully for correlation ID: {CorrelationId}",
                    command.CorrelationId);
            }
            else
            {
                // Handle failure - could retry or send to DLQ
                var failedEvent = new RequestFailed(
                    command.CorrelationId,
                    command.PartnerCode,
                    $"HTTP {response.StatusCode}",
                    null);

                await _publishEndpoint.Publish(failedEvent, context.CancellationToken);

                _logger.LogWarning("Request failed for correlation ID: {CorrelationId}, Status: {StatusCode}",
                    command.CorrelationId, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing DispatchToPartner for correlation ID: {CorrelationId}",
                command.CorrelationId);

            var failedEvent = new RequestFailed(
                command.CorrelationId,
                command.PartnerCode,
                ex.Message,
                null);

            await _publishEndpoint.Publish(failedEvent, context.CancellationToken);

            throw; // Let MassTransit handle retry/DLQ
        }
    }
}

