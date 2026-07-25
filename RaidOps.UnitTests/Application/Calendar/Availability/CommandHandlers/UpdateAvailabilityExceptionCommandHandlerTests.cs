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
    private readonly Mock<IGuildAccessService> _access = new();
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
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _repository.Setup(r => r.GetPatternsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _sut = new UpdateAvailabilityExceptionCommandHandler(_access.Object, _repository.Object, _announcer.Object);
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
        GuildId = GuildId,
        RequesterDiscordId = RequesterId,
        ExceptionId = ExceptionId,
        StartDate = startDate,
        EndDate = endDate,
        Status = DayAvailabilityStatus.Absent,
    };

    [Fact]
    public async Task HandleAsync_AccessBelowRoster_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Public);

        var result = await _sut.HandleAsync(MakeCommand(Today, Today));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_ExceptionNotFound_ReturnsAvailabilityExceptionNotFound()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _repository.Setup(r => r.GetExceptionByIdAsync(ExceptionId, RequesterId, GuildId, default)).ReturnsAsync((AvailabilityDeclaration?)null);

        var result = await _sut.HandleAsync(MakeCommand(Today, Today));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.AvailabilityExceptionNotFound);
    }

    [Fact]
    public async Task HandleAsync_ExistingAlreadyElapsed_ReturnsPastDeclarationLocked()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _repository.Setup(r => r.GetExceptionByIdAsync(ExceptionId, RequesterId, GuildId, default))
            .ReturnsAsync(MakeExisting(Today.AddDays(-5), Today.AddDays(-1)));

        var result = await _sut.HandleAsync(MakeCommand(Today, Today));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.PastDeclarationLocked);
    }

    [Fact]
    public async Task HandleAsync_NewEndDateBeforeNewStartDate_ReturnsInvalidRequest()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _repository.Setup(r => r.GetExceptionByIdAsync(ExceptionId, RequesterId, GuildId, default))
            .ReturnsAsync(MakeExisting(Today, Today));

        var result = await _sut.HandleAsync(MakeCommand(Today.AddDays(2), Today.AddDays(1)));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidRequest);
    }

    [Fact]
    public async Task HandleAsync_NewStartDateInThePast_ReturnsPastDeclarationLocked()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _repository.Setup(r => r.GetExceptionByIdAsync(ExceptionId, RequesterId, GuildId, default))
            .ReturnsAsync(MakeExisting(Today, Today));

        var result = await _sut.HandleAsync(MakeCommand(Today.AddDays(-1), Today));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.PastDeclarationLocked);
    }

    [Fact]
    public async Task HandleAsync_PartialWithoutEitherBound_ReturnsInvalidRequest()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _repository.Setup(r => r.GetExceptionByIdAsync(ExceptionId, RequesterId, GuildId, default))
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
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        var existing = MakeExisting(Today, Today.AddDays(4));
        _repository.Setup(r => r.GetExceptionByIdAsync(ExceptionId, RequesterId, GuildId, default)).ReturnsAsync(existing);
        _repository.Setup(r => r.DeleteExceptionAsync(ExceptionId, RequesterId, GuildId, default)).ReturnsAsync(true);
        _repository.Setup(r => r.AddExceptionAsync(It.IsAny<AvailabilityDeclaration>(), default))
            .ReturnsAsync((AvailabilityDeclaration e, CancellationToken _) => { e.Id = 99; return e; });

        // Shrinks the range to [Today, Today+1] — the announce window must still cover the
        // widest span (existing.EndDate = Today+4) so the diff sees the days that dropped off.
        var command = MakeCommand(Today, Today.AddDays(1));

        var result = await _sut.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Body.Should().BeEquivalentTo(new { Id = 99 });

        _repository.Verify(r => r.DeleteExceptionAsync(ExceptionId, RequesterId, GuildId, default), Times.Once);
        _repository.Verify(r => r.AddExceptionAsync(It.Is<AvailabilityDeclaration>(e =>
            e.UserDiscordId == RequesterId && e.GuildId == GuildId &&
            e.StartDate == command.StartDate && e.EndDate == command.EndDate),
            default), Times.Once);

        _announcer.Verify(a => a.AnnounceAsync(
            It.Is<AvailabilityChange>(c => c.WindowStart == Today && c.WindowEnd == Today.AddDays(4)),
            default), Times.Once);
    }
}
