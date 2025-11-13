namespace Inbound.Api.Presentation.Dtos;

/// <summary>
/// DTO de resposta para criação de requisição
/// </summary>
public record CreateRequestResponseDto
{
    public Guid CorrelationId { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
}

