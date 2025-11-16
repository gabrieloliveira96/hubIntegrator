using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Orchestrator.Worker.Services;

namespace Unit.Tests.Services;

public class BusinessRulesServiceTests
{
    private readonly Mock<ILogger<BusinessRulesService>> _loggerMock;
    private readonly BusinessRulesService _service;

    public BusinessRulesServiceTests()
    {
        _loggerMock = new Mock<ILogger<BusinessRulesService>>();
        _service = new BusinessRulesService(_loggerMock.Object);
    }

    [Fact]
    public async Task ValidateRequestAsync_ShouldReturnTrue()
    {
        // Arrange
        var partnerCode = "PARTNER01";
        var type = "ORDER";

        // Act
        var result = await _service.ValidateRequestAsync(partnerCode, type, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateRequestAsync_ShouldCompleteWithinReasonableTime()
    {
        // Arrange
        var partnerCode = "PARTNER01";
        var type = "ORDER";
        var startTime = DateTime.UtcNow;

        // Act
        await _service.ValidateRequestAsync(partnerCode, type, CancellationToken.None);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task EnrichRequestDataAsync_ShouldCompleteSuccessfully()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var partnerCode = "PARTNER01";

        // Act
        var act = async () => await _service.EnrichRequestDataAsync(correlationId, partnerCode, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnrichRequestDataAsync_ShouldCompleteWithinReasonableTime()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var partnerCode = "PARTNER01";
        var startTime = DateTime.UtcNow;

        // Act
        await _service.EnrichRequestDataAsync(correlationId, partnerCode, CancellationToken.None);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ValidateRequestAsync_WhenCancelled_ShouldThrowOperationCancelledException()
    {
        // Arrange
        var partnerCode = "PARTNER01";
        var type = "ORDER";
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await _service.ValidateRequestAsync(partnerCode, type, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task EnrichRequestDataAsync_WhenCancelled_ShouldThrowOperationCancelledException()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var partnerCode = "PARTNER01";
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await _service.EnrichRequestDataAsync(correlationId, partnerCode, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}

