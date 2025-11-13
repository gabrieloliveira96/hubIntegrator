namespace Inbound.Api.Presentation.Dtos;

/// <summary>
/// DTO de resposta para consulta de requisição
/// </summary>
public record GetRequestResponseDto
{
    public Guid CorrelationId { get; init; }
    public string PartnerCode { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
}

