using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Orchestrator.Worker.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace Integration.Tests;

public class OrchestratorSagaTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;
    private readonly RabbitMqContainer _rabbitMqContainer;
    private OrchestratorDbContext? _dbContext;

    public OrchestratorSagaTests()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("test_orchestrator")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        _rabbitMqContainer = new RabbitMqBuilder()
            .WithImage("rabbitmq:3-management-alpine")
            .WithUsername("guest")
            .WithPassword("guest")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();
        await _rabbitMqContainer.StartAsync();

        var options = new DbContextOptionsBuilder<OrchestratorDbContext>()
            .UseNpgsql(_postgresContainer.GetConnectionString())
            .Options;

        _dbContext = new OrchestratorDbContext(options);
        await _dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }
        await _postgresContainer.DisposeAsync();
        await _rabbitMqContainer.DisposeAsync();
    }

    [Fact]
    public async Task Database_ShouldBeAccessible()
    {
        // Arrange & Act
        var canConnect = await _dbContext!.Database.CanConnectAsync();

        // Assert
        canConnect.Should().BeTrue();
    }
}

