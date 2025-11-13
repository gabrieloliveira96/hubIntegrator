using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Shared.Persistence;

[Table("Outbox")]
public class OutboxMessage
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(500)]
    public string MessageType { get; set; } = string.Empty;

    [Required]
    public string Payload { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public bool Published { get; set; }

    [MaxLength(500)]
    public string? CorrelationId { get; set; }
}

public static class OutboxConfiguration
{
    public static void ConfigureOutbox(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasIndex(e => new { e.Published, e.CreatedAt });
            entity.HasIndex(e => e.CorrelationId);
        });
    }
}

