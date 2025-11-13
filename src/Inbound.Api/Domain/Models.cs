using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Inbound.Api.Domain;

[Table("Requests")]
public class Request
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string CorrelationId { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string PartnerCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Type { get; set; } = string.Empty;

    [Required]
    public string Payload { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string IdempotencyKey { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Status { get; set; } = "Received";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}

[Table("DedupKeys")]
public class DedupKey
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string CorrelationId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}

[Table("Nonces")]
public class Nonce
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Value { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
}

