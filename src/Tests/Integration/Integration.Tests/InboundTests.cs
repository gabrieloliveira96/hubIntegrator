using FluentAssertions;
using Inbound.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace Integration.Tests;

// Note: This test requires making Program class public or using a different approach
// For now, this is a placeholder structure

public class InboundTests : IClassFixture<InboundTestFixture>, IAsyncLifetime
{
    private readonly InboundTestFixture _fixture;
    private IServiceProvider? _serviceProvider;

    public InboundTests(InboundTestFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Setup service provider with test containers
        var services = new ServiceCollection();
        services.AddDbContext<InboxDbContext>(options =>
            options.UseNpgsql(_fixture.PostgresContainer.GetConnectionString()));

        _serviceProvider = services.BuildServiceProvider();

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InboxDbContext>();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Database_ShouldBeAccessible()
    {
        // Arrange
        using var scope = _serviceProvider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InboxDbContext>();

        // Act & Assert
        var canConnect = await db.Database.CanConnectAsync();
        canConnect.Should().BeTrue();
    }
}

public class InboundTestFixture : IAsyncLifetime
{
    public PostgreSqlContainer PostgresContainer { get; private set; } = null!;
    public RabbitMqContainer RabbitMqContainer { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        PostgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("test_db")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        RabbitMqContainer = new RabbitMqBuilder()
            .WithImage("rabbitmq:3-management-alpine")
            .WithUsername("guest")
            .WithPassword("guest")
            .Build();

        await PostgresContainer.StartAsync();
        await RabbitMqContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await PostgresContainer.DisposeAsync();
        await RabbitMqContainer.DisposeAsync();
    }
}

