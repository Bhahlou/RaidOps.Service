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

public class UpdateAvailabilityExceptionCommandHandlerTests
{
    private readonly Mock<IAvailabilityRepository> _repository = new();
    private readonly Mock<IAvailabilityChangeAnnouncer> _announcer = new();
    private readonly UpdateAvailabilityExceptionCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const string RequesterId = "user-1";
    private const int ExceptionId = 7;

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    public UpdateAvailabilityExceptionCommandHandlerTests()
    {
        _repository.Setup(r => r.GetExceptionsOverlappingAsync(
                It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _repository.Setup(r => r.GetPatternsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _sut = new UpdateAvailabilityExceptionCommandHandler(_repository.Object, _announcer.Object);
    }

    private static AvailabilityDeclaration MakeExisting(DateOnly startDate, DateOnly endDate) => new()
    {
        Id = ExceptionId,
        UserDiscordId = RequesterId,
        GuildId = GuildId,
        StartDate = startDate,
        EndDate = endDate,
        Status = DayAvailabilityStatus.Absent,
    };

    private static UpdateAvailabilityExceptionCommand MakeCommand(DateOnly startDate, DateOnly endDate) => new()
    {
        RequesterDiscordId = RequesterId,
        ExceptionId = ExceptionId,
        StartDate = startDate,
        EndDate = endDate,
        Status = DayAvailabilityStatus.Absent,
    };

    [Fact]
    public async Task HandleAsync_ExceptionNotFound_ReturnsAvailabilityExceptionNotFound()
    {
        _repository.Setup(r => r.GetExceptionByIdAsync(ExceptionId, RequesterId, default)).ReturnsAsync((AvailabilityDeclaration?)null);

        var result = await _sut.HandleAsync(MakeCommand(Today, Today));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.AvailabilityExceptionNotFound);
    }

    [Fact]
    public async Task HandleAsync_ExistingAlreadyElapsed_ReturnsPastDeclarationLocked()
    {
        _repository.Setup(r => r.GetExceptionByIdAsync(ExceptionId, RequesterId, default))
            .ReturnsAsync(MakeExisting(Today.AddDays(-5), Today.AddDays(-1)));

        var result = await _sut.HandleAsync(MakeCommand(Today, Today));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.PastDeclarationLocked);
    }

    [Fact]
    public async Task HandleAsync_NewEndDateBeforeNewStartDate_ReturnsInvalidRequest()
    {
        _repository.Setup(r => r.GetExceptionByIdAsync(ExceptionId, RequesterId, default))
            .ReturnsAsync(MakeExisting(Today, Today));

        var result = await _sut.HandleAsync(MakeCommand(Today.AddDays(2), Today.AddDays(1)));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidRequest);
    }

    [Fact]
    public async Task HandleAsync_NewStartDateInThePast_ReturnsPastDeclarationLocked()
    {
        _repository.Setup(r => r.GetExceptionByIdAsync(ExceptionId, RequesterId, default))
            .ReturnsAsync(MakeExisting(Today, Today));

        var result = await _sut.HandleAsync(MakeCommand(Today.AddDays(-1), Today));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.PastDeclarationLocked);
    }

    [Fact]
    public async Task HandleAsync_PartialWithoutEitherBound_ReturnsInvalidRequest()
    {
        _repository.Setup(r => r.GetExceptionByIdAsync(ExceptionId, RequesterId, default))
            .ReturnsAsync(MakeExisting(Today, Today));
        var command = MakeCommand(Today, Today);
        command.Status = DayAvailabilityStatus.Partial;

        var result = await _sut.HandleAsync(command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidRequest);
        _repository.Verify(r => r.AddExceptionAsync(It.IsAny<AvailabilityDeclaration>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Success_DeletesOldAddsNewAndAnnouncesWidestWindow()
    {
        var existing = MakeExisting(Today, Today.AddDays(4));
        _repository.Setup(r => r.GetExceptionByIdAsync(ExceptionId, RequesterId, default)).ReturnsAsync(existing);
        _repository.Setup(r => r.DeleteExceptionAsync(ExceptionId, RequesterId, default)).ReturnsAsync(true);
        _repository.Setup(r => r.AddExceptionAsync(It.IsAny<AvailabilityDeclaration>(), default))
            .ReturnsAsync((AvailabilityDeclaration e, CancellationToken _) => { e.Id = 99; return e; });

        // Shrinks the range to [Today, Today+1] — the announce window must still cover the
        // widest span (existing.EndDate = Today+4) so the diff sees the days that dropped off.
        var command = MakeCommand(Today, Today.AddDays(1));

        var result = await _sut.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Body.Should().BeEquivalentTo(new { Id = 99 });

        _repository.Verify(r => r.DeleteExceptionAsync(ExceptionId, RequesterId, default), Times.Once);
        _repository.Verify(r => r.AddExceptionAsync(It.Is<AvailabilityDeclaration>(e =>
            e.UserDiscordId == RequesterId && e.GuildId == GuildId &&
            e.StartDate == command.StartDate && e.EndDate == command.EndDate),
            default), Times.Once);

        _announcer.Verify(a => a.AnnounceAsync(
            It.Is<AvailabilityChange>(c => c.GuildId == GuildId && c.WindowStart == Today && c.WindowEnd == Today.AddDays(4)),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Success_ExpandedRangeAnnouncesWidestWindow()
    {
        var existing = MakeExisting(Today, Today.AddDays(2));
        _repository.Setup(r => r.GetExceptionByIdAsync(ExceptionId, RequesterId, default)).ReturnsAsync(existing);
        _repository.Setup(r => r.DeleteExceptionAsync(ExceptionId, RequesterId, default)).ReturnsAsync(true);
        _repository.Setup(r => r.AddExceptionAsync(It.IsAny<AvailabilityDeclaration>(), default))
            .ReturnsAsync((AvailabilityDeclaration e, CancellationToken _) => { e.Id = 99; return e; });

        // Shifts and expands the range to [Today+1, Today+5] — the announce window must still
        // cover the widest span: existing.StartDate (Today) on the left, command.EndDate
        // (Today+5) on the right.
        var command = MakeCommand(Today.AddDays(1), Today.AddDays(5));

        var result = await _sut.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();

        _announcer.Verify(a => a.AnnounceAsync(
            It.Is<AvailabilityChange>(c => c.GuildId == GuildId && c.WindowStart == Today && c.WindowEnd == Today.AddDays(5)),
            default), Times.Once);
    }
}
