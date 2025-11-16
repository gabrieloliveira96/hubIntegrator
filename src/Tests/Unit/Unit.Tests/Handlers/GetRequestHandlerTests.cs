using FluentAssertions;
using Inbound.Api.Application.Handlers;
using Inbound.Api.Application.Queries;
using Inbound.Api.Domain;
using Inbound.Api.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Moq;

namespace Unit.Tests.Handlers;

public class GetRequestHandlerTests
{
    private readonly Mock<IRequestRepository> _requestRepositoryMock;
    private readonly Mock<ILogger<GetRequestHandler>> _loggerMock;
    private readonly GetRequestHandler _handler;

    public GetRequestHandlerTests()
    {
        _requestRepositoryMock = new Mock<IRequestRepository>();
        _loggerMock = new Mock<ILogger<GetRequestHandler>>();

        _handler = new GetRequestHandler(
            _requestRepositoryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WhenRequestExists_ShouldReturnRequest()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var query = new GetRequestQuery(correlationId);

        var request = new Request
        {
            Id = Guid.NewGuid(),
            CorrelationId = correlationId.ToString(),
            PartnerCode = "PARTNER01",
            Type = "ORDER",
            Status = "Received",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };

        _requestRepositoryMock
            .Setup(x => x.GetByCorrelationIdAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.CorrelationId.Should().Be(correlationId);
        result.PartnerCode.Should().Be("PARTNER01");
        result.Type.Should().Be("ORDER");
        result.Status.Should().Be("Received");
        result.CreatedAt.Should().Be(request.CreatedAt);
        result.UpdatedAt.Should().Be(request.UpdatedAt);
    }

    [Fact]
    public async Task Handle_WhenRequestNotFound_ShouldReturnNull()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var query = new GetRequestQuery(correlationId);

        _requestRepositoryMock
            .Setup(x => x.GetByCorrelationIdAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Request?)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenRequestHasNoUpdatedAt_ShouldReturnNullForUpdatedAt()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var query = new GetRequestQuery(correlationId);

        var request = new Request
        {
            Id = Guid.NewGuid(),
            CorrelationId = correlationId.ToString(),
            PartnerCode = "PARTNER01",
            Type = "ORDER",
            Status = "Received",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = null
        };

        _requestRepositoryMock
            .Setup(x => x.GetByCorrelationIdAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.UpdatedAt.Should().BeNull();
    }
}

