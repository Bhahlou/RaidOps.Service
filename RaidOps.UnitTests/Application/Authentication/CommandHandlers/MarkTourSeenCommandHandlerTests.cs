using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Authentication.Commands;
using RaidOps.Application.Implementations.Authentication.CommandHandlers;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Authentication.CommandHandlers;

public class MarkTourSeenCommandHandlerTests
{
    private readonly Mock<ISeenTourRepository> _seenTours = new();
    private readonly MarkTourSeenCommandHandler _sut;

    private const string RequesterId = "user-1";

    public MarkTourSeenCommandHandlerTests()
    {
        _sut = new MarkTourSeenCommandHandler(_seenTours.Object);
    }

    [Fact]
    public async Task HandleAsync_Success_RecordsSeenTour()
    {
        var command = new MarkTourSeenCommand { RequesterDiscordId = RequesterId, TourId = "general" };

        var result = await _sut.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        _seenTours.Verify(r => r.MarkSeenAsync(RequesterId, "general", default), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_BlankTourId_FailsWithoutPersisting(string tourId)
    {
        var command = new MarkTourSeenCommand { RequesterDiscordId = RequesterId, TourId = tourId };

        var result = await _sut.HandleAsync(command);

        result.IsFailed.Should().BeTrue();
        _seenTours.Verify(r => r.MarkSeenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
