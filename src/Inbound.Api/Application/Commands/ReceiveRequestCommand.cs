using MediatR;

namespace Inbound.Api.Application.Commands;

/// <summary>
/// Command recebido do endpoint (sem IdempotencyKey, que vem do header)
/// </summary>
public record ReceiveRequestCommand(
    string PartnerCode,
    string Type,
    string Payload) : IRequest<ReceiveRequestCommandResponse>;

/// <summary>
/// Command usado pelo handler (com IdempotencyKey do header)
/// </summary>
public record ReceiveRequestCommandWithIdempotency(
    string PartnerCode,
    string Type,
    string Payload,
    string IdempotencyKey) : IRequest<ReceiveRequestCommandResponse>;

public record ReceiveRequestCommandResponse
{
    public Guid CorrelationId { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
}

