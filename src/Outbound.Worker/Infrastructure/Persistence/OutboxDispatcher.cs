using MassTransit;
using Microsoft.EntityFrameworkCore;
using Outbound.Worker.Infrastructure.Persistence;
using Shared.Contracts;
using System.Text.Json;

namespace Outbound.Worker.Infrastructure.Persistence;

public class OutboxDispatcher : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxDispatcher> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(5);

    public OutboxDispatcher(IServiceProvider serviceProvider, ILogger<OutboxDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox messages");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OutboxDbContext>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var pendingMessages = await dbContext.Outbox
            .Where(m => !m.Published)
            .OrderBy(m => m.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        foreach (var message in pendingMessages)
        {
            try
            {
                var eventType = GetEventType(message.MessageType);
                if (eventType == null)
                {
                    _logger.LogWarning("Unknown message type: {MessageType}", message.MessageType);
                    message.Published = true;
                    message.PublishedAt = DateTimeOffset.UtcNow;
                    continue;
                }

                var payload = JsonSerializer.Deserialize(message.Payload, eventType);
                if (payload != null)
                {
                    await publishEndpoint.Publish(payload, cancellationToken);
                    message.Published = true;
                    message.PublishedAt = DateTimeOffset.UtcNow;

                    _logger.LogInformation("Published outbox message {MessageId} of type {MessageType}",
                        message.Id, message.MessageType);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing outbox message {MessageId}", message.Id);
            }
        }

        if (pendingMessages.Any())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private Type? GetEventType(string messageType)
    {
        return messageType switch
        {
            nameof(RequestCompleted) => typeof(RequestCompleted),
            nameof(RequestFailed) => typeof(RequestFailed),
            _ => null
        };
    }
}

