using System.ComponentModel.DataAnnotations;

namespace Inbound.Api.Presentation.Dtos;

/// <summary>
/// DTO de entrada para criação de requisição
/// </summary>
public record CreateRequestDto
{
    [Required]
    [MaxLength(50)]
    public string PartnerCode { get; init; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Type { get; init; } = string.Empty;

    [Required]
    public string Payload { get; init; } = string.Empty;
}

