using FluentAssertions;
using Inbound.Api.Application.Commands;
using Inbound.Api.Application.Handlers;
using Inbound.Api.Domain;
using Inbound.Api.Domain.Repositories;
using Inbound.Api.Domain.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Contracts;
using Shared.Persistence;

namespace Unit.Tests.Handlers;

public class ReceiveRequestHandlerTests
{
    private readonly Mock<IIdempotencyStore> _idempotencyStoreMock;
    private readonly Mock<IMqPublisher> _mqPublisherMock;
    private readonly Mock<IRequestRepository> _requestRepositoryMock;
    private readonly Mock<IInboxRepository> _inboxRepositoryMock;
    private readonly Mock<ILogger<ReceiveRequestHandler>> _loggerMock;
    private readonly ReceiveRequestHandler _handler;

    public ReceiveRequestHandlerTests()
    {
        _idempotencyStoreMock = new Mock<IIdempotencyStore>();
        _mqPublisherMock = new Mock<IMqPublisher>();
        _requestRepositoryMock = new Mock<IRequestRepository>();
        _inboxRepositoryMock = new Mock<IInboxRepository>();
        _loggerMock = new Mock<ILogger<ReceiveRequestHandler>>();

        _handler = new ReceiveRequestHandler(
            _idempotencyStoreMock.Object,
            _mqPublisherMock.Object,
            _requestRepositoryMock.Object,
            _inboxRepositoryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WhenIdempotencyKeyExists_ShouldReturnExistingRequest()
    {
        // Arrange
        var existingCorrelationId = Guid.NewGuid();
        var idempotencyKey = "test-key-123";
        var command = new ReceiveRequestCommandWithIdempotency(
            "PARTNER01",
            "ORDER",
            "{\"orderId\":\"123\"}",
            idempotencyKey);

        var existingRequest = new Request
        {
            Id = Guid.NewGuid(),
            CorrelationId = existingCorrelationId.ToString(),
            PartnerCode = "PARTNER01",
            Type = "ORDER",
            Status = "Received",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };

        _idempotencyStoreMock
            .Setup(x => x.GetExistingCorrelationIdAsync(idempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCorrelationId);

        _requestRepositoryMock
            .Setup(x => x.GetByCorrelationIdAsync(existingCorrelationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRequest);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.CorrelationId.Should().Be(existingCorrelationId);
        result.Status.Should().Be("Received");
        result.CreatedAt.Should().Be(existingRequest.CreatedAt);

        _requestRepositoryMock.Verify(x => x.CreateAsync(It.IsAny<Request>(), It.IsAny<CancellationToken>()), Times.Never);
        _mqPublisherMock.Verify(x => x.PublishRequestReceivedAsync(It.IsAny<Request>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenIdempotencyKeyExistsButRequestNotFound_ShouldThrowException()
    {
        // Arrange
        var existingCorrelationId = Guid.NewGuid();
        var idempotencyKey = "test-key-123";
        var command = new ReceiveRequestCommandWithIdempotency(
            "PARTNER01",
            "ORDER",
            "{\"orderId\":\"123\"}",
            idempotencyKey);

        _idempotencyStoreMock
            .Setup(x => x.GetExistingCorrelationIdAsync(idempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCorrelationId);

        _requestRepositoryMock
            .Setup(x => x.GetByCorrelationIdAsync(existingCorrelationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Request?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenNewRequest_ShouldCreateRequestAndPublishEvent()
    {
        // Arrange
        var idempotencyKey = "new-key-123";
        var command = new ReceiveRequestCommandWithIdempotency(
            "PARTNER01",
            "ORDER",
            "{\"orderId\":\"123\"}",
            idempotencyKey);

        _idempotencyStoreMock
            .Setup(x => x.GetExistingCorrelationIdAsync(idempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        _requestRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Request r, CancellationToken ct) => r);

        _idempotencyStoreMock
            .Setup(x => x.CreateDedupKeyAsync(idempotencyKey, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, Guid corrId, CancellationToken ct) => corrId);

        _inboxRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<InboxMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InboxMessage m, CancellationToken ct) => m);

        _mqPublisherMock
            .Setup(x => x.PublishRequestReceivedAsync(It.IsAny<Request>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.CorrelationId.Should().NotBeEmpty();
        result.Status.Should().Be("Received");
        result.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));

        _requestRepositoryMock.Verify(
            x => x.CreateAsync(It.Is<Request>(r => 
                r.PartnerCode == "PARTNER01" && 
                r.Type == "ORDER" && 
                r.IdempotencyKey == idempotencyKey), 
            It.IsAny<CancellationToken>()), 
            Times.Once);

        _idempotencyStoreMock.Verify(
            x => x.CreateDedupKeyAsync(idempotencyKey, result.CorrelationId, It.IsAny<CancellationToken>()), 
            Times.Once);

        _inboxRepositoryMock.Verify(
            x => x.AddAsync(It.IsAny<InboxMessage>(), It.IsAny<CancellationToken>()), 
            Times.Once);

        _mqPublisherMock.Verify(
            x => x.PublishRequestReceivedAsync(It.IsAny<Request>(), It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenPayloadIsNotJson_ShouldStoreAsString()
    {
        // Arrange
        var idempotencyKey = "new-key-123";
        var nonJsonPayload = "plain text payload";
        var command = new ReceiveRequestCommandWithIdempotency(
            "PARTNER01",
            "ORDER",
            nonJsonPayload,
            idempotencyKey);

        _idempotencyStoreMock
            .Setup(x => x.GetExistingCorrelationIdAsync(idempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        _requestRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Request r, CancellationToken ct) => r);

        _idempotencyStoreMock
            .Setup(x => x.CreateDedupKeyAsync(idempotencyKey, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, Guid corrId, CancellationToken ct) => corrId);

        _inboxRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<InboxMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InboxMessage m, CancellationToken ct) => m);

        _mqPublisherMock
            .Setup(x => x.PublishRequestReceivedAsync(It.IsAny<Request>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        _inboxRepositoryMock.Verify(
            x => x.AddAsync(It.Is<InboxMessage>(m => m.Payload.Contains(nonJsonPayload)), It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenPayloadIsValidJson_ShouldParseAndStore()
    {
        // Arrange
        var idempotencyKey = "new-key-123";
        var jsonPayload = "{\"orderId\":\"123\",\"customerId\":\"CUST001\"}";
        var command = new ReceiveRequestCommandWithIdempotency(
            "PARTNER01",
            "ORDER",
            jsonPayload,
            idempotencyKey);

        _idempotencyStoreMock
            .Setup(x => x.GetExistingCorrelationIdAsync(idempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        _requestRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Request r, CancellationToken ct) => r);

        _idempotencyStoreMock
            .Setup(x => x.CreateDedupKeyAsync(idempotencyKey, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, Guid corrId, CancellationToken ct) => corrId);

        _inboxRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<InboxMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InboxMessage m, CancellationToken ct) => m);

        _mqPublisherMock
            .Setup(x => x.PublishRequestReceivedAsync(It.IsAny<Request>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        _inboxRepositoryMock.Verify(
            x => x.AddAsync(It.Is<InboxMessage>(m => 
                m.MessageType == nameof(RequestReceived) && 
                m.Payload.Contains("CorrelationId")), 
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }
}

