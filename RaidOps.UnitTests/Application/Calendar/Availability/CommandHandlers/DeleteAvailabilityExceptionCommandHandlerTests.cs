using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Calendar.Availability.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Calendar.Availability.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Calendar;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Calendar.Availability.CommandHandlers;

public class DeleteAvailabilityExceptionCommandHandlerTests
{
    private readonly Mock<IAvailabilityRepository> _repository = new();
    private readonly Mock<IAvailabilityChangeAnnouncer> _announcer = new();
    private readonly DeleteAvailabilityExceptionCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const string RequesterId = "user-1";
    private const int ExceptionId = 7;

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static readonly DeleteAvailabilityExceptionCommand Command = new()
    {
        RequesterDiscordId = RequesterId,
        ExceptionId = ExceptionId,
    };

    public DeleteAvailabilityExceptionCommandHandlerTests()
    {
        _repository.Setup(r => r.GetExceptionsOverlappingAsync(
                It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _repository.Setup(r => r.GetPatternsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _sut = new DeleteAvailabilityExceptionCommandHandler(_repository.Object, _announcer.Object);
    }

    private static AvailabilityDeclaration MakeException(DateOnly endDate) => new()
    {
        Id = ExceptionId, UserDiscordId = RequesterId, GuildId = GuildId,
        StartDate = endDate, EndDate = endDate, Status = DayAvailabilityStatus.Absent,
    };

    [Fact]
    public async Task HandleAsync_ExceptionNotFound_ReturnsAvailabilityExceptionNotFound()
    {
        _repository.Setup(r => r.GetExceptionByIdAsync(ExceptionId, RequesterId, default)).ReturnsAsync((AvailabilityDeclaration?)null);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.AvailabilityExceptionNotFound);
    }

    [Fact]
    public async Task HandleAsync_ExceptionAlreadyElapsed_ReturnsPastDeclarationLocked()
    {
        _repository.Setup(r => r.GetExceptionByIdAsync(ExceptionId, RequesterId, default))
            .ReturnsAsync(MakeException(Today.AddDays(-1)));

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.PastDeclarationLocked);
    }

    [Fact]
    public async Task HandleAsync_ExceptionEndsToday_IsNotLockedAndSucceeds()
    {
        _repository.Setup(r => r.GetExceptionByIdAsync(ExceptionId, RequesterId, default))
            .ReturnsAsync(MakeException(Today));
        _repository.Setup(r => r.DeleteExceptionAsync(ExceptionId, RequesterId, default)).ReturnsAsync(true);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_DeleteRaceLosesAfterExceptionWasFound_ReturnsAvailabilityExceptionNotFound()
    {
        _repository.Setup(r => r.GetExceptionByIdAsync(ExceptionId, RequesterId, default))
            .ReturnsAsync(MakeException(Today));
        _repository.Setup(r => r.DeleteExceptionAsync(ExceptionId, RequesterId, default)).ReturnsAsync(false);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.AvailabilityExceptionNotFound);
    }

    [Fact]
    public async Task HandleAsync_Success_CallsDeleteExceptionWithExpectedIdsAndReturnsOk()
    {
        _repository.Setup(r => r.GetExceptionByIdAsync(ExceptionId, RequesterId, default))
            .ReturnsAsync(MakeException(Today.AddDays(1)));
        _repository.Setup(r => r.DeleteExceptionAsync(ExceptionId, RequesterId, default)).ReturnsAsync(true);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _repository.Verify(r => r.DeleteExceptionAsync(ExceptionId, RequesterId, default), Times.Once);
    }
}
