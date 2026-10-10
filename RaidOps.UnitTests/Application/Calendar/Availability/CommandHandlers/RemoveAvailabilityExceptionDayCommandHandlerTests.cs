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

public class RemoveAvailabilityExceptionDayCommandHandlerTests
{
    private readonly Mock<IAvailabilityRepository> _repository = new();
    private readonly Mock<IAvailabilityChangeAnnouncer> _announcer = new();
    private readonly RemoveAvailabilityExceptionDayCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const string RequesterId = "user-1";
    private const int ExceptionId = 7;

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    public RemoveAvailabilityExceptionDayCommandHandlerTests()
    {
        _repository.Setup(r => r.GetExceptionsOverlappingAsync(
                It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _repository.Setup(r => r.GetPatternsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _repository.Setup(r => r.DeleteExceptionAsync(ExceptionId, RequesterId, default)).ReturnsAsync(true);
        _repository.Setup(r => r.AddExceptionAsync(It.IsAny<AvailabilityDeclaration>(), default))
            .ReturnsAsync((AvailabilityDeclaration e, CancellationToken _) => e);

        _sut = new RemoveAvailabilityExceptionDayCommandHandler(_repository.Object, _announcer.Object);
    }

    private static AvailabilityDeclaration MakeExisting(DateOnly startDate, DateOnly endDate) => new()
    {
        Id = ExceptionId,
        UserDiscordId = RequesterId,
        GuildId = GuildId,
        StartDate = startDate,
        EndDate = endDate,
        Status = DayAvailabilityStatus.Absent,
        Reason = "Vacation",
    };

    private static RemoveAvailabilityExceptionDayCommand MakeCommand(DateOnly date) => new()
    {
        RequesterDiscordId = RequesterId,
        ExceptionId = ExceptionId,
        Date = date,
    };

    [Fact]
    public async Task HandleAsync_ExceptionNotFound_ReturnsAvailabilityExceptionNotFound()
    {
        _repository.Setup(r => r.GetExceptionByIdAsync(ExceptionId, RequesterId, default)).ReturnsAsync((AvailabilityDeclaration?)null);

        var result = await _sut.HandleAsync(MakeCommand(Today));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.AvailabilityExceptionNotFound);
    }

    [Fact]
    public async Task HandleAsync_ExistingAlreadyElapsed_ReturnsPastDeclarationLocked()
    {
        _repository.Setup(r => r.GetExceptionByIdAsync(ExceptionId, RequesterId, default))
            .ReturnsAsync(MakeExisting(Today.AddDays(-5), Today.AddDays(-1)));

        var result = await _sut.HandleAsync(MakeCommand(Today.AddDays(-3)));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.PastDeclarationLocked);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public async Task HandleAsync_DateOutsideExceptionRange_ReturnsInvalidRequest(int dayOffset)
    {
        _repository.Setup(r => r.GetExceptionByIdAsync(ExceptionId, RequesterId, default))
            .ReturnsAsync(MakeExisting(Today, Today.AddDays(5)));

        var result = await _sut.HandleAsync(MakeCommand(Today.AddDays(dayOffset)));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidRequest);
    }

    [Fact]
    public async Task HandleAsync_SingleDayException_DeletesWithNoReplacementFragments()
    {
        _repository.Setup(r => r.GetExceptionByIdAsync(ExceptionId, RequesterId, default))
            .ReturnsAsync(MakeExisting(Today, Today));

        var result = await _sut.HandleAsync(MakeCommand(Today));

        result.IsSuccess.Should().BeTrue();
        _repository.Verify(r => r.DeleteExceptionAsync(ExceptionId, RequesterId, default), Times.Once);
        _repository.Verify(r => r.AddExceptionAsync(It.IsAny<AvailabilityDeclaration>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_RemoveStartEdgeDay_ShrinksFromStart()
    {
        _repository.Setup(r => r.GetExceptionByIdAsync(ExceptionId, RequesterId, default))
            .ReturnsAsync(MakeExisting(Today, Today.AddDays(4)));

        var result = await _sut.HandleAsync(MakeCommand(Today));

        result.IsSuccess.Should().BeTrue();
        _repository.Verify(r => r.AddExceptionAsync(It.Is<AvailabilityDeclaration>(e =>
            e.StartDate == Today.AddDays(1) && e.EndDate == Today.AddDays(4) && e.Reason == "Vacation"),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_RemoveEndEdgeDay_ShrinksFromEnd()
    {
        _repository.Setup(r => r.GetExceptionByIdAsync(ExceptionId, RequesterId, default))
            .ReturnsAsync(MakeExisting(Today, Today.AddDays(4)));

        var result = await _sut.HandleAsync(MakeCommand(Today.AddDays(4)));

        result.IsSuccess.Should().BeTrue();
        _repository.Verify(r => r.AddExceptionAsync(It.Is<AvailabilityDeclaration>(e =>
            e.StartDate == Today && e.EndDate == Today.AddDays(3)),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_RemoveMiddleDay_SplitsIntoTwoFragments()
    {
        _repository.Setup(r => r.GetExceptionByIdAsync(ExceptionId, RequesterId, default))
            .ReturnsAsync(MakeExisting(Today, Today.AddDays(4)));

        var result = await _sut.HandleAsync(MakeCommand(Today.AddDays(2)));

        result.IsSuccess.Should().BeTrue();
        _repository.Verify(r => r.AddExceptionAsync(It.Is<AvailabilityDeclaration>(e =>
            e.StartDate == Today && e.EndDate == Today.AddDays(1)),
            default), Times.Once);
        _repository.Verify(r => r.AddExceptionAsync(It.Is<AvailabilityDeclaration>(e =>
            e.StartDate == Today.AddDays(3) && e.EndDate == Today.AddDays(4)),
            default), Times.Once);
    }
}
