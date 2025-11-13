using System.Text.Json;

namespace Shared.Contracts;

public record ReceiveRequest(
    Guid CorrelationId,
    string PartnerCode,
    string Type,
    JsonElement Payload,
    DateTimeOffset CreatedAt);

