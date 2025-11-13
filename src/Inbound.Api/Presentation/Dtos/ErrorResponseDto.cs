namespace Inbound.Api.Presentation.Dtos;

/// <summary>
/// DTO de resposta de erro
/// </summary>
public record ErrorResponseDto
{
    public string Error { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public object? Example { get; init; }
    public string? HowToFix { get; init; }
}

