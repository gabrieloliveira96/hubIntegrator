using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Orchestrator.Worker.Infrastructure.Persistence;
using Orchestrator.Worker.Sagas;
using Shared.Contracts;
using System.Text.Json;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace Integration.Tests;

public class OrchestratorSagaTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;
    private readonly RabbitMqContainer _rabbitMqContainer;
    private OrchestratorDbContext? _dbContext;
    private IBusControl? _bus;
    private IServiceProvider? _serviceProvider;

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

        // Setup MassTransit for testing
        var services = new ServiceCollection();
        services.AddDbContext<OrchestratorDbContext>(opt => opt.UseNpgsql(_postgresContainer.GetConnectionString()));
        services.AddMassTransit(x =>
        {
            x.AddSagaStateMachine<RequestSaga, SagaStateMap>()
                .EntityFrameworkRepository(r =>
                {
                    r.ExistingDbContext<OrchestratorDbContext>();
                    r.UsePostgres();
                });

            x.UsingRabbitMq((context, cfg) =>
            {
                var uri = new Uri(_rabbitMqContainer.GetConnectionString());
                cfg.Host(uri.Host, (ushort)uri.Port, "/", h =>
                {
                    h.Username("guest");
                    h.Password("guest");
                });
                cfg.ConfigureEndpoints(context);
            });
        });

        _serviceProvider = services.BuildServiceProvider();
        _bus = _serviceProvider.GetRequiredService<IBusControl>();
        await _bus.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_bus != null)
        {
            await _bus.StopAsync();
        }
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
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

    [Fact]
    public async Task Saga_WhenRequestReceived_ShouldCreateSagaInstance()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var requestReceived = new RequestReceived(
            correlationId,
            "PARTNER01",
            "ORDER",
            JsonSerializer.Deserialize<JsonElement>("{\"orderId\":\"123\"}"),
            DateTimeOffset.UtcNow);

        var publishEndpoint = _serviceProvider!.GetRequiredService<IPublishEndpoint>();

        // Act
        await publishEndpoint.Publish(requestReceived, CancellationToken.None);
        
        // Wait a bit for saga to process
        await Task.Delay(2000);

        // Assert
        var saga = await _dbContext!.SagaStates
            .FirstOrDefaultAsync(s => s.CorrelationId == correlationId);
        
        saga.Should().NotBeNull();
        saga!.PartnerCode.Should().Be("PARTNER01");
        saga.RequestType.Should().Be("ORDER");
        saga.CurrentState.Should().Be("Processing"); // Should transition to Processing
    }

    [Fact]
    public async Task Saga_WhenRequestCompleted_ShouldTransitionToSucceeded()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var requestReceived = new RequestReceived(
            correlationId,
            "PARTNER01",
            "ORDER",
            JsonSerializer.Deserialize<JsonElement>("{\"orderId\":\"123\"}"),
            DateTimeOffset.UtcNow);

        var publishEndpoint = _serviceProvider!.GetRequiredService<IPublishEndpoint>();

        // Act - Publish RequestReceived
        await publishEndpoint.Publish(requestReceived, CancellationToken.None);
        await Task.Delay(1000);

        // Publish RequestCompleted
        var requestCompleted = new RequestCompleted(
            correlationId,
            "PARTNER01",
            200,
            "Success",
            JsonSerializer.Deserialize<JsonElement>("{\"result\":\"ok\"}"));
        
        await publishEndpoint.Publish(requestCompleted, CancellationToken.None);
        await Task.Delay(1000);

        // Assert
        var saga = await _dbContext!.SagaStates
            .FirstOrDefaultAsync(s => s.CorrelationId == correlationId);
        
        saga.Should().NotBeNull();
        saga!.CurrentState.Should().Be("Succeeded");
        saga.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Saga_WhenRequestFailed_ShouldTransitionToFailed()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var requestReceived = new RequestReceived(
            correlationId,
            "PARTNER01",
            "ORDER",
            JsonSerializer.Deserialize<JsonElement>("{\"orderId\":\"123\"}"),
            DateTimeOffset.UtcNow);

        var publishEndpoint = _serviceProvider!.GetRequiredService<IPublishEndpoint>();

        // Act - Publish RequestReceived
        await publishEndpoint.Publish(requestReceived, CancellationToken.None);
        await Task.Delay(1000);

        // Publish RequestFailed
        var requestFailed = new RequestFailed(
            correlationId,
            "PARTNER01",
            "Connection timeout",
            null);
        
        await publishEndpoint.Publish(requestFailed, CancellationToken.None);
        await Task.Delay(1000);

        // Assert
        var saga = await _dbContext!.SagaStates
            .FirstOrDefaultAsync(s => s.CorrelationId == correlationId);
        
        saga.Should().NotBeNull();
        saga!.CurrentState.Should().Be("Failed");
        saga.UpdatedAt.Should().NotBeNull();
    }
}

