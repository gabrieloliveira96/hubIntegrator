using MassTransit;
using Microsoft.EntityFrameworkCore;
using Shared.Persistence;

namespace Orchestrator.Worker.Infrastructure.Persistence;

public class OrchestratorDbContext : DbContext
{
    public OrchestratorDbContext(DbContextOptions<OrchestratorDbContext> options) : base(options)
    {
    }

    public DbSet<SagaStateMap> SagaStates { get; set; } = null!;
    public DbSet<OutboxMessage> Outbox { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SagaStateMap>(entity =>
        {
            entity.ToTable("Sagas");
            entity.HasKey(e => e.CorrelationId);
            entity.HasIndex(e => e.CurrentState);
            
            // Configure RowVersion for optimistic concurrency
            // PostgreSQL doesn't support IsRowVersion() the same way SQL Server does
            // Use bytea for PostgreSQL compatibility - MassTransit will handle versioning
            entity.Property(e => e.RowVersion)
                .IsRequired()
                .HasColumnType("bytea")
                .HasDefaultValue(Array.Empty<byte>());
            
            // Ensure CurrentState has a default value to prevent empty states
            entity.Property(e => e.CurrentState)
                .HasDefaultValue("Initial")
                .IsRequired();
        });

        modelBuilder.ConfigureOutbox();
    }
}

public class SagaStateMap : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = "Initial"; // Default to Initial to prevent empty state errors
    public string PartnerCode { get; set; } = string.Empty;
    public string RequestType { get; set; } = string.Empty;
    public string? Payload { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

