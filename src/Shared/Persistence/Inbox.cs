using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Shared.Persistence;

[Table("Inbox")]
public class InboxMessage
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(500)]
    public string MessageId { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string MessageType { get; set; } = string.Empty;

    [Required]
    public string Payload { get; set; } = string.Empty;

    public DateTimeOffset ReceivedAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public bool Processed { get; set; }

    [MaxLength(500)]
    public string? CorrelationId { get; set; }
}

public static class InboxConfiguration
{
    public static void ConfigureInbox(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.HasIndex(e => e.MessageId)
                .IsUnique();
            entity.HasIndex(e => new { e.Processed, e.ReceivedAt });
            entity.HasIndex(e => e.CorrelationId);
        });
    }
}

