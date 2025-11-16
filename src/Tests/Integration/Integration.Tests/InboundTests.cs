using FluentAssertions;
using Inbound.Api.Domain;
using Inbound.Api.Infrastructure.Persistence;
using Inbound.Api.Presentation.Dtos;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;
using Xunit;

namespace Integration.Tests;

public class InboundTests : IClassFixture<InboundTestFixture>, IAsyncLifetime
{
    private readonly InboundTestFixture _fixture;
    private HttpClient? _httpClient;
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

        // Create HTTP client for API testing with custom factory
        var factory = new InboundWebApplicationFactory(
            _fixture.PostgresContainer,
            _fixture.RabbitMqContainer,
            _fixture.RedisContainer);
        _httpClient = factory.CreateClient();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private void AddRequiredHeaders(HttpClient client, string idempotencyKey)
    {
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);
        client.DefaultRequestHeaders.Add("X-Nonce", Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add("X-Timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
    }

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

    [Fact]
    public async Task CreateRequest_WithoutIdempotencyKey_ShouldReturnBadRequest()
    {
        // Arrange
        var dto = new CreateRequestDto
        {
            PartnerCode = "PARTNER01",
            Type = "ORDER",
            Payload = "{\"orderId\":\"123\"}"
        };

        // Act
        var response = await _httpClient!.PostAsJsonAsync("/requests", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorJson = await response.Content.ReadAsStringAsync();
        errorJson.Should().ContainAny("Idempotency-Key", "Missing required headers", "X-Nonce", "X-Timestamp");
    }

    [Fact]
    public async Task CreateRequest_WithIdempotencyKey_ShouldCreateRequest()
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid().ToString();
        var dto = new CreateRequestDto
        {
            PartnerCode = "PARTNER01",
            Type = "ORDER",
            Payload = "{\"orderId\":\"123\"}"
        };

        AddRequiredHeaders(_httpClient!, idempotencyKey);

        // Act
        var response = await _httpClient!.PostAsJsonAsync("/requests", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var result = await response.Content.ReadFromJsonAsync<CreateRequestResponseDto>();
        result.Should().NotBeNull();
        result!.CorrelationId.Should().NotBeEmpty();
        result.Status.Should().Be("Received");
    }

    [Fact]
    public async Task CreateRequest_WithDuplicateIdempotencyKey_ShouldReturnExistingRequest()
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid().ToString();
        var dto = new CreateRequestDto
        {
            PartnerCode = "PARTNER01",
            Type = "ORDER",
            Payload = "{\"orderId\":\"123\"}"
        };

        AddRequiredHeaders(_httpClient!, idempotencyKey);

        // Act - First request
        var firstResponse = await _httpClient.PostAsJsonAsync("/requests", dto);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var firstResult = await firstResponse.Content.ReadFromJsonAsync<CreateRequestResponseDto>();
        var correlationId = firstResult!.CorrelationId;

        // Act - Duplicate request
        var secondResponse = await _httpClient.PostAsJsonAsync("/requests", dto);

        // Assert
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var secondResult = await secondResponse.Content.ReadFromJsonAsync<CreateRequestResponseDto>();
        secondResult.Should().NotBeNull();
        secondResult!.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task GetRequest_WhenRequestExists_ShouldReturnRequest()
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid().ToString();
        var dto = new CreateRequestDto
        {
            PartnerCode = "PARTNER01",
            Type = "ORDER",
            Payload = "{\"orderId\":\"123\"}"
        };

        AddRequiredHeaders(_httpClient!, idempotencyKey);

        var createResponse = await _httpClient!.PostAsJsonAsync("/requests", dto);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateRequestResponseDto>();
        var correlationId = createResult!.CorrelationId;

        // Act
        var getResponse = await _httpClient.GetAsync($"/requests/{correlationId}");

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await getResponse.Content.ReadFromJsonAsync<GetRequestResponseDto>();
        result.Should().NotBeNull();
        result!.CorrelationId.Should().Be(correlationId);
        result.PartnerCode.Should().Be("PARTNER01");
        result.Type.Should().Be("ORDER");
    }

    [Fact]
    public async Task GetRequest_WhenRequestNotFound_ShouldReturnNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _httpClient!.GetAsync($"/requests/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}


public class InboundTestFixture : IAsyncLifetime
{
    public PostgreSqlContainer PostgresContainer { get; private set; } = null!;
    public RabbitMqContainer RabbitMqContainer { get; private set; } = null!;
    public RedisContainer RedisContainer { get; private set; } = null!;

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

        RedisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();

        await PostgresContainer.StartAsync();
        await RabbitMqContainer.StartAsync();
        await RedisContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await PostgresContainer.DisposeAsync();
        await RabbitMqContainer.DisposeAsync();
        await RedisContainer.DisposeAsync();
    }
}

