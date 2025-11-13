using Microsoft.EntityFrameworkCore;
using Shared.Persistence;

namespace Outbound.Worker.Infrastructure.Persistence;

public class OutboxDbContext : DbContext
{
    public OutboxDbContext(DbContextOptions<OutboxDbContext> options) : base(options)
    {
    }

    public DbSet<OutboxMessage> Outbox { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureOutbox();
    }
}

