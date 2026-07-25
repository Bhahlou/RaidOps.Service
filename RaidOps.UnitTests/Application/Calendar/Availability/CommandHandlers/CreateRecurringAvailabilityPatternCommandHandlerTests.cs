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

public class CreateRecurringAvailabilityPatternCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IAvailabilityRepository> _repository = new();
    private readonly Mock<IAuditLogService> _auditLog = new();
    private readonly Mock<IGuildNotificationDispatcher> _notificationDispatcher = new();
    private readonly Mock<IAbsenceNotificationContentBuilder> _absenceContentBuilder = new();
    private readonly CreateRecurringAvailabilityPatternCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const string RequesterId = "user-1";

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly DateOnly Anchor = new(2026, 1, 5);

    public CreateRecurringAvailabilityPatternCommandHandlerTests()
    {
        _sut = new CreateRecurringAvailabilityPatternCommandHandler(_access.Object, _repository.Object, _auditLog.Object, _notificationDispatcher.Object, _absenceContentBuilder.Object);
    }

    private static CreateRecurringAvailabilityPatternCommand MakeCommand(int cycleLengthDays, List<RecurringAvailabilityPatternDayInput> days) => new()
    {
        GuildId = GuildId,
        RequesterDiscordId = RequesterId,
        Label = "Weekly raid nights",
        CycleLengthDays = cycleLengthDays,
        AnchorDate = Anchor,
        Days = days,
    };

    [Fact]
    public async Task HandleAsync_AccessBelowRoster_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Public);
        var command = MakeCommand(7, []);

        var result = await _sut.HandleAsync(command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task HandleAsync_CycleLengthDaysNotPositive_ReturnsInvalidRequest(int cycleLengthDays)
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        var command = MakeCommand(cycleLengthDays, []);

        var result = await _sut.HandleAsync(command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidRequest);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    public async Task HandleAsync_DayOffsetOutsideCycleLength_ReturnsInvalidRequest(int offsetInCycle)
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        var command = MakeCommand(7, [new RecurringAvailabilityPatternDayInput { OffsetInCycle = offsetInCycle, Status = DayAvailabilityStatus.Absent }]);

        var result = await _sut.HandleAsync(command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidRequest);
    }

    [Fact]
    public async Task HandleAsync_PartialDayWithoutEitherBound_ReturnsInvalidRequest()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        var command = MakeCommand(7, [new RecurringAvailabilityPatternDayInput { OffsetInCycle = 0, Status = DayAvailabilityStatus.Partial }]);

        var result = await _sut.HandleAsync(command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidRequest);
        _repository.Verify(r => r.AddPatternAsync(It.IsAny<RecurringAvailabilityPattern>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Success_CallsAddPatternWithEffectiveFromTodayAndOpenEffectiveUntilAndMapsDays()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        var days = new List<RecurringAvailabilityPatternDayInput>
        {
            new() { OffsetInCycle = 2, Status = DayAvailabilityStatus.Absent, Reason = "Wednesday off" },
            new() { OffsetInCycle = 4, Status = DayAvailabilityStatus.Partial, AvailableFrom = new TimeOnly(18, 0), AvailableUntil = new TimeOnly(22, 0) },
        };
        var command = MakeCommand(7, days);
        _repository.Setup(r => r.AddPatternAsync(It.IsAny<RecurringAvailabilityPattern>(), default))
            .ReturnsAsync((RecurringAvailabilityPattern p, CancellationToken _) => { p.Id = 99; return p; });

        var result = await _sut.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Body.Should().BeEquivalentTo(new { Id = 99 });
        _repository.Verify(r => r.AddPatternAsync(It.Is<RecurringAvailabilityPattern>(p =>
            p.UserDiscordId == RequesterId && p.GuildId == GuildId && p.Label == "Weekly raid nights" &&
            p.CycleLengthDays == 7 && p.AnchorDate == Anchor &&
            p.EffectiveFrom == Today && p.EffectiveUntil == null &&
            p.Days.Count == 2 &&
            p.Days.Any(d => d.OffsetInCycle == 2 && d.Status == DayAvailabilityStatus.Absent && d.Reason == "Wednesday off") &&
            p.Days.Any(d => d.OffsetInCycle == 4 && d.Status == DayAvailabilityStatus.Partial &&
                             d.AvailableFrom == new TimeOnly(18, 0) && d.AvailableUntil == new TimeOnly(22, 0))),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Success_BuildsPatternEmbedFromSubmittedDaysAndNotifiesAbsenceAdded()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        var days = new List<RecurringAvailabilityPatternDayInput>
        {
            new() { OffsetInCycle = 2, Status = DayAvailabilityStatus.Absent, Reason = "Wednesday off" },
            new() { OffsetInCycle = 4, Status = DayAvailabilityStatus.Partial, AvailableFrom = new TimeOnly(18, 0), AvailableUntil = new TimeOnly(22, 0) },
        };
        var command = MakeCommand(7, days);
        _repository.Setup(r => r.AddPatternAsync(It.IsAny<RecurringAvailabilityPattern>(), default))
            .ReturnsAsync((RecurringAvailabilityPattern p, CancellationToken _) => { p.Id = 99; return p; });
        var embed = new DiscordEmbedContent("New recurring absences");
        _absenceContentBuilder.Setup(b => b.BuildPatternAsync(
                GuildId, RequesterId, GuildNotificationEventType.AbsenceAdded, Anchor, 7,
                It.IsAny<IReadOnlyList<PatternDayNotification>>(), default))
            .ReturnsAsync(embed);

        await _sut.HandleAsync(command);

        _absenceContentBuilder.Verify(b => b.BuildPatternAsync(
            GuildId, RequesterId, GuildNotificationEventType.AbsenceAdded, Anchor, 7,
            It.Is<IReadOnlyList<PatternDayNotification>>(mapped =>
                mapped.Count == 2 &&
                mapped.Any(d => d.OffsetInCycle == 2 && d.Status == DayAvailabilityStatus.Absent && d.Reason == "Wednesday off") &&
                mapped.Any(d => d.OffsetInCycle == 4 && d.Status == DayAvailabilityStatus.Partial &&
                                 d.AvailableFrom == new TimeOnly(18, 0) && d.AvailableUntil == new TimeOnly(22, 0))),
            default), Times.Once);

        _notificationDispatcher.Verify(d => d.NotifyAsync(GuildId, GuildNotificationEventType.AbsenceAdded, embed, default), Times.Once);
    }
}
