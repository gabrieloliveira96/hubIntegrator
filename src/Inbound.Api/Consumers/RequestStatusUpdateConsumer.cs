using Inbound.Api.Domain.Repositories;
using MassTransit;
using Shared.Contracts;

namespace Inbound.Api.Consumers;

/// <summary>
/// Consumer que atualiza o status da Request quando recebe eventos de conclusão ou falha
/// </summary>
public class RequestStatusUpdateConsumer : IConsumer<RequestCompleted>, IConsumer<RequestFailed>
{
    private readonly IRequestRepository _requestRepository;
    private readonly ILogger<RequestStatusUpdateConsumer> _logger;

    public RequestStatusUpdateConsumer(
        IRequestRepository requestRepository,
        ILogger<RequestStatusUpdateConsumer> logger)
    {
        _requestRepository = requestRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<RequestCompleted> context)
    {
        var message = context.Message;
        _logger.LogInformation("Updating request status to Completed for correlation ID: {CorrelationId}",
            message.CorrelationId);

        var request = await _requestRepository.GetByCorrelationIdAsync(message.CorrelationId, context.CancellationToken);

        if (request == null)
        {
            _logger.LogWarning("Request not found for correlation ID: {CorrelationId}", message.CorrelationId);
            return;
        }

        request.Status = message.Status; // "Completed"
        request.UpdatedAt = DateTimeOffset.UtcNow;

        await _requestRepository.UpdateAsync(request, context.CancellationToken);

        _logger.LogInformation("Request status updated to {Status} for correlation ID: {CorrelationId}",
            request.Status, message.CorrelationId);
    }

    public async Task Consume(ConsumeContext<RequestFailed> context)
    {
        var message = context.Message;
        _logger.LogInformation("Updating request status to Failed for correlation ID: {CorrelationId}",
            message.CorrelationId);

        var request = await _requestRepository.GetByCorrelationIdAsync(message.CorrelationId, context.CancellationToken);

        if (request == null)
        {
            _logger.LogWarning("Request not found for correlation ID: {CorrelationId}", message.CorrelationId);
            return;
        }

        request.Status = "Failed";
        request.UpdatedAt = DateTimeOffset.UtcNow;

        await _requestRepository.UpdateAsync(request, context.CancellationToken);

        _logger.LogInformation("Request status updated to Failed for correlation ID: {CorrelationId}. Reason: {Reason}",
            message.CorrelationId, message.Reason);
    }
}

