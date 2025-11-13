using Inbound.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Shared.Persistence;

namespace Inbound.Api.Infrastructure.Persistence;

public class InboxDbContext : DbContext
{
    public InboxDbContext(DbContextOptions<InboxDbContext> options) : base(options)
    {
    }

    public DbSet<Request> Requests { get; set; } = null!;
    public DbSet<DedupKey> DedupKeys { get; set; } = null!;
    public DbSet<Nonce> Nonces { get; set; } = null!;
    public DbSet<InboxMessage> Inbox { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Request>(entity =>
        {
            entity.HasIndex(e => e.CorrelationId)
                .IsUnique()
                .HasDatabaseName("IX_Requests_CorrelationId");

            entity.HasIndex(e => e.IdempotencyKey)
                .HasDatabaseName("IX_Requests_IdemKey");

            entity.HasIndex(e => new { e.PartnerCode, e.CreatedAt })
                .HasDatabaseName("IX_Requests_PartnerCode_CreatedAt");
        });

        modelBuilder.Entity<DedupKey>(entity =>
        {
            entity.HasIndex(e => e.Key)
                .IsUnique()
                .HasDatabaseName("IX_DedupKeys_Key");
        });

        modelBuilder.Entity<Nonce>(entity =>
        {
            entity.HasIndex(e => e.Value)
                .IsUnique()
                .HasDatabaseName("IX_Nonces_Value");

            entity.HasIndex(e => e.ExpiresAt)
                .HasDatabaseName("IX_Nonces_ExpiresAt");
        });

        modelBuilder.ConfigureInbox();
    }
}

