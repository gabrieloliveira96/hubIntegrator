using System.Text.Json;

namespace Shared.Contracts;

public record RequestReceived(
    Guid CorrelationId,
    string PartnerCode,
    string Type,
    JsonElement Payload,
    DateTimeOffset CreatedAt);

public record DispatchToPartner(
    Guid CorrelationId,
    string PartnerCode,
    Uri Endpoint,
    JsonElement Payload);

public record RequestCompleted(
    Guid CorrelationId,
    string PartnerCode,
    int StatusCode,
    string Status,
    JsonElement? Response);

public record RequestFailed(
    Guid CorrelationId,
    string PartnerCode,
    string Reason,
    int? Attempts);

