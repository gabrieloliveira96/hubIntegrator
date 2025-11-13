using Inbound.Api.Application.Commands;
using Inbound.Api.Domain;
using Inbound.Api.Infrastructure.Messaging;
using Inbound.Api.Infrastructure.Persistence;
using Inbound.Api.Infrastructure.Persistence.Repositories;
using MediatR;
using Shared.Contracts;
using Shared.Persistence;

namespace Inbound.Api.Application.Handlers;

public class ReceiveRequestHandler : IRequestHandler<ReceiveRequestCommandWithIdempotency, ReceiveRequestCommandResponse>
{
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly IMqPublisher _mqPublisher;
    private readonly IRequestRepository _requestRepository;
    private readonly IInboxRepository _inboxRepository;
    private readonly ILogger<ReceiveRequestHandler> _logger;

    public ReceiveRequestHandler(
        IIdempotencyStore idempotencyStore,
        IMqPublisher mqPublisher,
        IRequestRepository requestRepository,
        IInboxRepository inboxRepository,
        ILogger<ReceiveRequestHandler> logger)
    {
        _idempotencyStore = idempotencyStore;
        _mqPublisher = mqPublisher;
        _requestRepository = requestRepository;
        _inboxRepository = inboxRepository;
        _logger = logger;
    }

    public async Task<ReceiveRequestCommandResponse> Handle(
        ReceiveRequestCommandWithIdempotency request,
        CancellationToken cancellationToken)
    {
        // Check idempotency
        var existingCorrelationId = await _idempotencyStore.GetExistingCorrelationIdAsync(
            request.IdempotencyKey, cancellationToken);

        if (existingCorrelationId.HasValue)
        {
            _logger.LogWarning("Duplicate idempotency key: {IdempotencyKey}. Existing correlation ID: {CorrelationId}", 
                request.IdempotencyKey, existingCorrelationId.Value);
            
            // Return existing request instead of throwing
            var existingRequest = await _requestRepository.GetByCorrelationIdAsync(
                existingCorrelationId.Value, cancellationToken);
            
            if (existingRequest == null)
            {
                _logger.LogError("Correlation ID {CorrelationId} not found in database", existingCorrelationId.Value);
                throw new InvalidOperationException($"Request with correlation ID '{existingCorrelationId.Value}' not found");
            }
            
            return new ReceiveRequestCommandResponse
            {
                CorrelationId = existingCorrelationId.Value,
                Status = existingRequest.Status,
                CreatedAt = existingRequest.CreatedAt
            };
        }

        // Generate new correlation ID
        var correlationId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        // Create Request entity
        var requestEntity = new Request
        {
            Id = Guid.NewGuid(),
            CorrelationId = correlationId.ToString(),
            PartnerCode = request.PartnerCode,
            Type = request.Type,
            Payload = request.Payload,
            IdempotencyKey = request.IdempotencyKey,
            Status = "Received",
            CreatedAt = now
        };

        // Save Request using repository
        await _requestRepository.CreateAsync(requestEntity, cancellationToken);

        // Create DedupKey for idempotency
        await _idempotencyStore.CreateDedupKeyAsync(
            request.IdempotencyKey,
            correlationId,
            cancellationToken);

        // Persist to Inbox
        // The payload comes as a string that may or may not be valid JSON
        // We'll store it as-is in the inbox message payload
        object payloadValue;
        try
        {
            if (!string.IsNullOrWhiteSpace(request.Payload))
            {
                // Try to parse as JSON first
                var trimmedPayload = request.Payload.Trim();
                if ((trimmedPayload.StartsWith("{") && trimmedPayload.EndsWith("}")) ||
                    (trimmedPayload.StartsWith("[") && trimmedPayload.EndsWith("]")))
                {
                    // It looks like JSON, try to parse it
                    payloadValue = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(request.Payload);
                }
                else
                {
                    // Not JSON, store as string
                    payloadValue = request.Payload;
                }
            }
            else
            {
                payloadValue = request.Payload ?? string.Empty;
            }
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogWarning("Payload is not valid JSON, storing as string. Error: {Error}", ex.Message);
            // If payload is not valid JSON, store it as a string
            payloadValue = request.Payload;
        }

        var inboxMessage = new InboxMessage
        {
            Id = Guid.NewGuid(),
            MessageId = Guid.NewGuid().ToString(),
            MessageType = nameof(RequestReceived),
            Payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                CorrelationId = correlationId,
                request.PartnerCode,
                request.Type,
                Payload = payloadValue
            }),
            ReceivedAt = DateTimeOffset.UtcNow,
            CorrelationId = correlationId.ToString()
        };

        await _inboxRepository.AddAsync(inboxMessage, cancellationToken);

        // Publish message (use the requestEntity we just created and saved)
        await _mqPublisher.PublishRequestReceivedAsync(requestEntity, cancellationToken);

        _logger.LogInformation("Request created successfully. CorrelationId: {CorrelationId}", correlationId);

        return new ReceiveRequestCommandResponse
        {
            CorrelationId = correlationId,
            Status = "Received",
            CreatedAt = requestEntity.CreatedAt
        };
    }
}

