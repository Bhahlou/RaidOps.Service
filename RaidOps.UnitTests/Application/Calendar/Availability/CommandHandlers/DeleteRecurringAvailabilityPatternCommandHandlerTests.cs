using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Calendar.Availability.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Calendar.Availability.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Calendar;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Calendar.Availability.CommandHandlers;

public class DeleteRecurringAvailabilityPatternCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IAvailabilityRepository> _repository = new();
    private readonly Mock<IAuditLogService> _auditLog = new();
    private readonly Mock<IGuildNotificationDispatcher> _notificationDispatcher = new();
    private readonly Mock<IAbsenceNotificationContentBuilder> _absenceContentBuilder = new();
    private readonly DeleteRecurringAvailabilityPatternCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const string RequesterId = "user-1";
    private const int PatternId = 5;

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly DateOnly Anchor = new(2026, 1, 5);

    private static readonly DeleteRecurringAvailabilityPatternCommand Command = new()
    {
        GuildId = GuildId,
        RequesterDiscordId = RequesterId,
        PatternId = PatternId,
    };

    public DeleteRecurringAvailabilityPatternCommandHandlerTests()
    {
        _sut = new DeleteRecurringAvailabilityPatternCommandHandler(_access.Object, _repository.Object, _auditLog.Object, _notificationDispatcher.Object, _absenceContentBuilder.Object);
    }

    private static RecurringAvailabilityPattern MakeExistingPattern(DateOnly effectiveFrom) => new()
    {
        Id = PatternId, UserDiscordId = RequesterId, GuildId = GuildId,
        CycleLengthDays = 7, AnchorDate = Anchor, EffectiveFrom = effectiveFrom, EffectiveUntil = null,
        Days =
        [
            new RecurringAvailabilityPatternDay { OffsetInCycle = 0, Status = DayAvailabilityStatus.Absent },
            new RecurringAvailabilityPatternDay
            {
                OffsetInCycle = 4, Status = DayAvailabilityStatus.Partial,
                AvailableFrom = new TimeOnly(18, 0), AvailableUntil = new TimeOnly(22, 0),
            },
        ],
    };

    [Fact]
    public async Task HandleAsync_AccessBelowRoster_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Public);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_PatternNotFound_ReturnsRecurringAvailabilityPatternNotFound()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _repository.Setup(r => r.GetPatternByIdAsync(PatternId, RequesterId, GuildId, default)).ReturnsAsync((RecurringAvailabilityPattern?)null);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RecurringAvailabilityPatternNotFound);
    }

    [Fact]
    public async Task HandleAsync_EffectiveFromInPast_ClosesPatternAndNeverDeletesThenReturnsOk()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _repository.Setup(r => r.GetPatternByIdAsync(PatternId, RequesterId, GuildId, default))
            .ReturnsAsync(MakeExistingPattern(Today.AddDays(-5)));
        _repository.Setup(r => r.ClosePatternAsync(PatternId, RequesterId, GuildId, Today.AddDays(-1), default)).ReturnsAsync(true);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _repository.Verify(r => r.ClosePatternAsync(PatternId, RequesterId, GuildId, Today.AddDays(-1), default), Times.Once);
        _repository.Verify(r => r.DeletePatternAsync(PatternId, RequesterId, GuildId, default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_EffectiveFromToday_DeletesPatternAndNeverClosesThenReturnsOk()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _repository.Setup(r => r.GetPatternByIdAsync(PatternId, RequesterId, GuildId, default))
            .ReturnsAsync(MakeExistingPattern(Today));
        _repository.Setup(r => r.DeletePatternAsync(PatternId, RequesterId, GuildId, default)).ReturnsAsync(true);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _repository.Verify(r => r.DeletePatternAsync(PatternId, RequesterId, GuildId, default), Times.Once);
        _repository.Verify(r => r.ClosePatternAsync(PatternId, RequesterId, GuildId, It.IsAny<DateOnly>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Success_LogsAuditVariablesIncludingEachDaysStatusAndTimes()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _repository.Setup(r => r.GetPatternByIdAsync(PatternId, RequesterId, GuildId, default))
            .ReturnsAsync(MakeExistingPattern(Today));
        _repository.Setup(r => r.DeletePatternAsync(PatternId, RequesterId, GuildId, default)).ReturnsAsync(true);

        await _sut.HandleAsync(Command);

        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.RecurringAvailabilityPatternStopped,
            It.Is<Dictionary<string, string>>(v =>
                v["days"].Contains("\"offsetInCycle\":0") &&
                v["days"].Contains("\"status\":\"Absent\"") &&
                v["days"].Contains("\"availableFrom\":\"18:00:00\"")),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Success_BuildsPatternEmbedFromExistingDaysAndNotifiesAbsenceRemoved()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _repository.Setup(r => r.GetPatternByIdAsync(PatternId, RequesterId, GuildId, default))
            .ReturnsAsync(MakeExistingPattern(Today));
        _repository.Setup(r => r.DeletePatternAsync(PatternId, RequesterId, GuildId, default)).ReturnsAsync(true);
        var embed = new DiscordEmbedContent("Recurring absences removed");
        _absenceContentBuilder.Setup(b => b.BuildPatternAsync(
                GuildId, RequesterId, GuildNotificationEventType.AbsenceRemoved, Anchor, 7,
                It.IsAny<IReadOnlyList<PatternDayNotification>>(), default))
            .ReturnsAsync(embed);

        await _sut.HandleAsync(Command);

        _absenceContentBuilder.Verify(b => b.BuildPatternAsync(
            GuildId, RequesterId, GuildNotificationEventType.AbsenceRemoved, Anchor, 7,
            It.Is<IReadOnlyList<PatternDayNotification>>(mapped =>
                mapped.Count == 2 &&
                mapped.Any(d => d.OffsetInCycle == 0 && d.Status == DayAvailabilityStatus.Absent) &&
                mapped.Any(d => d.OffsetInCycle == 4 && d.Status == DayAvailabilityStatus.Partial &&
                                 d.AvailableFrom == new TimeOnly(18, 0) && d.AvailableUntil == new TimeOnly(22, 0))),
            default), Times.Once);

        _notificationDispatcher.Verify(d => d.NotifyAsync(GuildId, GuildNotificationEventType.AbsenceRemoved, embed, default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_StopCallReturnsFalse_ReturnsRecurringAvailabilityPatternNotFound()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _repository.Setup(r => r.GetPatternByIdAsync(PatternId, RequesterId, GuildId, default))
            .ReturnsAsync(MakeExistingPattern(Today));
        _repository.Setup(r => r.DeletePatternAsync(PatternId, RequesterId, GuildId, default)).ReturnsAsync(false);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RecurringAvailabilityPatternNotFound);
    }
}
