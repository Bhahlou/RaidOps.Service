using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Calendar.Availability.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Calendar.Availability.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Calendar;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

namespace RaidOps.UnitTests.Application.Calendar.Availability.Services;

public class AvailabilityChangeAnnouncerTests
{
    private readonly Mock<IAvailabilityResolutionService> _resolution = new();
    private readonly Mock<IActiveRosterBranchResolver> _activeRosterResolver = new();
    private readonly Mock<IAuditLogService> _auditLog = new();
    private readonly Mock<IGuildNotificationDispatcher> _dispatcher = new();
    private readonly Mock<IAbsenceNotificationContentBuilder> _contentBuilder = new();
    private readonly AvailabilityChangeAnnouncer _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "user-1";

    private static readonly DateOnly Day1 = new(2026, 7, 1);
    private static readonly DateOnly Day2 = new(2026, 7, 2);
    private static readonly DateOnly Day3 = new(2026, 7, 3);

    // Moq matches IEnumerable arguments by sequence content, not reference — two empty lists
    // would be indistinguishable and collide, so each is seeded with a distinct marker element.
    private readonly List<AvailabilityDeclaration> _beforeExceptions = [new() { Id = -1 }];
    private readonly List<AvailabilityDeclaration> _afterExceptions = [new() { Id = -2 }];
    private readonly List<RecurringAvailabilityPattern> _patterns = [];

    public AvailabilityChangeAnnouncerTests()
    {
        _contentBuilder.Setup(b => b.GetGuildLanguageAsync(It.IsAny<string>(), default)).ReturnsAsync("en");
        _activeRosterResolver.Setup(r => r.GetActiveBranchesAsync(RequesterId, default)).ReturnsAsync([]);
        _sut = new AvailabilityChangeAnnouncer(_resolution.Object, _activeRosterResolver.Object, _auditLog.Object, _dispatcher.Object, _contentBuilder.Object);
    }

    private static ResolvedDayAvailabilityResponse Resolved(
        DateOnly date,
        DayAvailabilityStatus status,
        TimeOnly? from = null,
        TimeOnly? until = null) => new()
    {
        Date = date,
        Status = status,
        AvailableFrom = from,
        AvailableUntil = until,
    };

    private void SetupResolveForScope(
        string? guildId, int? guildBranchId, DateOnly windowStart, DateOnly windowEnd,
        List<ResolvedDayAvailabilityResponse> before, List<ResolvedDayAvailabilityResponse> after)
    {
        _resolution.Setup(r => r.ResolveForScope(windowStart, windowEnd, _beforeExceptions, _patterns, guildId, guildBranchId)).Returns(before);
        _resolution.Setup(r => r.ResolveForScope(windowStart, windowEnd, _afterExceptions, _patterns, guildId, guildBranchId)).Returns(after);
    }

    private void SetupResolve(DateOnly windowStart, DateOnly windowEnd, List<ResolvedDayAvailabilityResponse> before, List<ResolvedDayAvailabilityResponse> after)
        => SetupResolveForScope(GuildId, GuildBranchId, windowStart, windowEnd, before, after);

    private AvailabilityChange MakeChange(DateOnly windowStart, DateOnly windowEnd) => new(
        GuildId, GuildBranchId, RequesterId, windowStart, windowEnd, _beforeExceptions, _afterExceptions, _patterns);

    private AvailabilityChange MakeGlobalChange(DateOnly windowStart, DateOnly windowEnd) => new(
        null, null, RequesterId, windowStart, windowEnd, _beforeExceptions, _afterExceptions, _patterns);

    [Fact]
    public async Task AnnounceAsync_NothingChanged_NoAuditLogOrNotification()
    {
        SetupResolve(Day1, Day1,
            before: [Resolved(Day1, DayAvailabilityStatus.Available)],
            after: [Resolved(Day1, DayAvailabilityStatus.Available)]);

        await _sut.AnnounceAsync(MakeChange(Day1, Day1));

        _auditLog.Verify(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GuildAuditAction>(), It.IsAny<Dictionary<string, string>>(), default), Times.Never);
        _dispatcher.Verify(d => d.NotifyAsync(It.IsAny<string>(), It.IsAny<GuildNotificationEventType>(), It.IsAny<int?>(), It.IsAny<DiscordEmbedContent>(), default), Times.Never);
        _contentBuilder.Verify(b => b.GetGuildLanguageAsync(It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task AnnounceAsync_SingleDayBecomesAbsent_LogsDeclaredAndNotifiesAbsenceAdded()
    {
        SetupResolve(Day1, Day1,
            before: [Resolved(Day1, DayAvailabilityStatus.Available)],
            after: [Resolved(Day1, DayAvailabilityStatus.Absent)]);

        await _sut.AnnounceAsync(MakeChange(Day1, Day1));

        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.AvailabilityExceptionDeclared,
            It.Is<Dictionary<string, string>>(v =>
                v["startDate"] == "2026-07-01" && v["endDate"] == "2026-07-01" &&
                v["status"] == "Absent" && v["availableFrom"] == "" && v["availableUntil"] == ""),
            default), Times.Once);

        _contentBuilder.Verify(b => b.BuildAsync(
            GuildId, RequesterId, GuildNotificationEventType.AbsenceAdded, AbsenceKind.FullDay,
            It.Is<IReadOnlyList<DiscordEmbedField>>(f => f.Single().Name == "Dates" && f.Single().Value == "7/1/2026"),
            default), Times.Once);

        _dispatcher.Verify(d => d.NotifyAsync(GuildId, GuildNotificationEventType.AbsenceAdded, GuildBranchId, It.IsAny<DiscordEmbedContent>(), default), Times.Once);
    }

    [Fact]
    public async Task AnnounceAsync_SingleDayBecomesAvailable_LogsDeletedAndNotifiesAbsenceRemoved()
    {
        SetupResolve(Day1, Day1,
            before: [Resolved(Day1, DayAvailabilityStatus.Absent)],
            after: [Resolved(Day1, DayAvailabilityStatus.Available)]);

        await _sut.AnnounceAsync(MakeChange(Day1, Day1));

        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.AvailabilityExceptionDeleted,
            It.IsAny<Dictionary<string, string>>(), default), Times.Once);

        _contentBuilder.Verify(b => b.BuildAsync(
            GuildId, RequesterId, GuildNotificationEventType.AbsenceRemoved, AbsenceKind.FullDay,
            It.IsAny<IReadOnlyList<DiscordEmbedField>>(), default), Times.Once);

        _dispatcher.Verify(d => d.NotifyAsync(GuildId, GuildNotificationEventType.AbsenceRemoved, GuildBranchId, It.IsAny<DiscordEmbedContent>(), default), Times.Once);
    }

    [Fact]
    public async Task AnnounceAsync_ContiguousDaysSameStatus_MergeIntoOneSegment()
    {
        SetupResolve(Day1, Day3,
            before: [Resolved(Day1, DayAvailabilityStatus.Available), Resolved(Day2, DayAvailabilityStatus.Available), Resolved(Day3, DayAvailabilityStatus.Available)],
            after: [Resolved(Day1, DayAvailabilityStatus.Absent), Resolved(Day2, DayAvailabilityStatus.Absent), Resolved(Day3, DayAvailabilityStatus.Absent)]);

        await _sut.AnnounceAsync(MakeChange(Day1, Day3));

        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.AvailabilityExceptionDeclared,
            It.Is<Dictionary<string, string>>(v => v["startDate"] == "2026-07-01" && v["endDate"] == "2026-07-03"),
            default), Times.Once);

        _dispatcher.Verify(d => d.NotifyAsync(It.IsAny<string>(), It.IsAny<GuildNotificationEventType>(), It.IsAny<int?>(), It.IsAny<DiscordEmbedContent>(), default), Times.Once);
    }

    [Fact]
    public async Task AnnounceAsync_NonContiguousChanges_ProduceSeparateSegments()
    {
        // Day2 stays restricted both before and after (unchanged) — it must not bridge Day1 and Day3
        // into a single segment.
        SetupResolve(Day1, Day3,
            before: [Resolved(Day1, DayAvailabilityStatus.Available), Resolved(Day2, DayAvailabilityStatus.Absent), Resolved(Day3, DayAvailabilityStatus.Available)],
            after: [Resolved(Day1, DayAvailabilityStatus.Absent), Resolved(Day2, DayAvailabilityStatus.Absent), Resolved(Day3, DayAvailabilityStatus.Absent)]);

        await _sut.AnnounceAsync(MakeChange(Day1, Day3));

        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.AvailabilityExceptionDeclared,
            It.Is<Dictionary<string, string>>(v => v["startDate"] == "2026-07-01" && v["endDate"] == "2026-07-01"),
            default), Times.Once);
        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.AvailabilityExceptionDeclared,
            It.Is<Dictionary<string, string>>(v => v["startDate"] == "2026-07-03" && v["endDate"] == "2026-07-03"),
            default), Times.Once);

        _dispatcher.Verify(d => d.NotifyAsync(It.IsAny<string>(), It.IsAny<GuildNotificationEventType>(), It.IsAny<int?>(), It.IsAny<DiscordEmbedContent>(), default), Times.Exactly(2));
    }

    [Fact]
    public async Task AnnounceAsync_ContiguousDaysWithDifferentPartialTimes_DoNotMerge()
    {
        SetupResolve(Day1, Day2,
            before: [Resolved(Day1, DayAvailabilityStatus.Available), Resolved(Day2, DayAvailabilityStatus.Available)],
            after:
            [
                Resolved(Day1, DayAvailabilityStatus.Partial, from: new TimeOnly(18, 0)),
                Resolved(Day2, DayAvailabilityStatus.Partial, from: new TimeOnly(20, 0)),
            ]);

        await _sut.AnnounceAsync(MakeChange(Day1, Day2));

        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.AvailabilityExceptionDeclared,
            It.Is<Dictionary<string, string>>(v => v["startDate"] == "2026-07-01" && v["endDate"] == "2026-07-01" && v["availableFrom"] == "18:00:00"),
            default), Times.Once);
        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.AvailabilityExceptionDeclared,
            It.Is<Dictionary<string, string>>(v => v["startDate"] == "2026-07-02" && v["endDate"] == "2026-07-02" && v["availableFrom"] == "20:00:00"),
            default), Times.Once);
    }

    [Fact]
    public async Task AnnounceAsync_DayStaysRestrictedButStatusChanges_NoSegmentReported()
    {
        // Absent -> Partial: both sides are "restricted", so the diff engine treats this as
        // unchanged from a notification standpoint, by design — this locks that behavior in.
        SetupResolve(Day1, Day1,
            before: [Resolved(Day1, DayAvailabilityStatus.Absent)],
            after: [Resolved(Day1, DayAvailabilityStatus.Partial, from: new TimeOnly(9, 0))]);

        await _sut.AnnounceAsync(MakeChange(Day1, Day1));

        _auditLog.Verify(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GuildAuditAction>(), It.IsAny<Dictionary<string, string>>(), default), Times.Never);
        _dispatcher.Verify(d => d.NotifyAsync(It.IsAny<string>(), It.IsAny<GuildNotificationEventType>(), It.IsAny<int?>(), It.IsAny<DiscordEmbedContent>(), default), Times.Never);
    }

    [Fact]
    public async Task AnnounceAsync_PartialWindowSegment_BuildsPartialWindowKind()
    {
        SetupResolve(Day1, Day1,
            before: [Resolved(Day1, DayAvailabilityStatus.Available)],
            after: [Resolved(Day1, DayAvailabilityStatus.Partial, from: new TimeOnly(9, 0), until: new TimeOnly(17, 0))]);

        await _sut.AnnounceAsync(MakeChange(Day1, Day1));

        _contentBuilder.Verify(b => b.BuildAsync(
            GuildId, RequesterId, GuildNotificationEventType.AbsenceAdded, AbsenceKind.PartialWindow,
            It.Is<IReadOnlyList<DiscordEmbedField>>(f => f.Single().Value == "7/1/2026 · 09:00 – 17:00"),
            default), Times.Once);
    }

    [Fact]
    public async Task AnnounceAsync_ContiguousDaysOppositeDirection_DoNotMerge()
    {
        // Day1 flips restricted->available (removed) while the adjacent Day2 flips
        // available->restricted (added) — same date adjacency as a mergeable run, but opposite
        // IsAdded, so they must stay two separate segments.
        SetupResolve(Day1, Day2,
            before: [Resolved(Day1, DayAvailabilityStatus.Absent), Resolved(Day2, DayAvailabilityStatus.Available)],
            after: [Resolved(Day1, DayAvailabilityStatus.Available), Resolved(Day2, DayAvailabilityStatus.Absent)]);

        await _sut.AnnounceAsync(MakeChange(Day1, Day2));

        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.AvailabilityExceptionDeleted,
            It.Is<Dictionary<string, string>>(v => v["startDate"] == "2026-07-01" && v["endDate"] == "2026-07-01"),
            default), Times.Once);
        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.AvailabilityExceptionDeclared,
            It.Is<Dictionary<string, string>>(v => v["startDate"] == "2026-07-02" && v["endDate"] == "2026-07-02"),
            default), Times.Once);
    }

    [Fact]
    public async Task AnnounceAsync_ContiguousDaysDifferentStatus_DoNotMerge()
    {
        // Same direction (both added) and contiguous, but the resulting status itself differs
        // (Absent vs Partial) — must not be folded into a single segment.
        SetupResolve(Day1, Day2,
            before: [Resolved(Day1, DayAvailabilityStatus.Available), Resolved(Day2, DayAvailabilityStatus.Available)],
            after:
            [
                Resolved(Day1, DayAvailabilityStatus.Absent),
                Resolved(Day2, DayAvailabilityStatus.Partial, from: new TimeOnly(9, 0)),
            ]);

        await _sut.AnnounceAsync(MakeChange(Day1, Day2));

        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.AvailabilityExceptionDeclared,
            It.Is<Dictionary<string, string>>(v => v["startDate"] == "2026-07-01" && v["endDate"] == "2026-07-01" && v["status"] == "Absent"),
            default), Times.Once);
        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.AvailabilityExceptionDeclared,
            It.Is<Dictionary<string, string>>(v => v["startDate"] == "2026-07-02" && v["endDate"] == "2026-07-02" && v["status"] == "Partial"),
            default), Times.Once);
    }

    [Fact]
    public async Task AnnounceAsync_ContiguousDaysDifferentAvailableUntil_DoNotMerge()
    {
        // Same status shape (Partial, AvailableFrom null) and contiguous, but AvailableUntil
        // itself differs between the two days — must not be folded into a single segment.
        SetupResolve(Day1, Day2,
            before: [Resolved(Day1, DayAvailabilityStatus.Available), Resolved(Day2, DayAvailabilityStatus.Available)],
            after:
            [
                Resolved(Day1, DayAvailabilityStatus.Partial, until: new TimeOnly(17, 0)),
                Resolved(Day2, DayAvailabilityStatus.Partial, until: new TimeOnly(18, 0)),
            ]);

        await _sut.AnnounceAsync(MakeChange(Day1, Day2));

        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.AvailabilityExceptionDeclared,
            It.Is<Dictionary<string, string>>(v => v["startDate"] == "2026-07-01" && v["endDate"] == "2026-07-01" && v["availableUntil"] == "17:00:00"),
            default), Times.Once);
        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.AvailabilityExceptionDeclared,
            It.Is<Dictionary<string, string>>(v => v["startDate"] == "2026-07-02" && v["endDate"] == "2026-07-02" && v["availableUntil"] == "18:00:00"),
            default), Times.Once);
    }

    [Fact]
    public async Task AnnounceAsync_ContiguousDaysAvailableFromGoesFromSetToUnset_DoNotMerge()
    {
        // Same Status (Partial) and contiguous, but AvailableFrom flips from set to unset between
        // the two days (LateArrival on Day1, EarlyLeave on Day2) — a HasValue mismatch, distinct
        // from the "both set but different" case already covered above.
        SetupResolve(Day1, Day2,
            before: [Resolved(Day1, DayAvailabilityStatus.Available), Resolved(Day2, DayAvailabilityStatus.Available)],
            after:
            [
                Resolved(Day1, DayAvailabilityStatus.Partial, from: new TimeOnly(9, 0)),
                Resolved(Day2, DayAvailabilityStatus.Partial, until: new TimeOnly(17, 0)),
            ]);

        await _sut.AnnounceAsync(MakeChange(Day1, Day2));

        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.AvailabilityExceptionDeclared,
            It.Is<Dictionary<string, string>>(v => v["startDate"] == "2026-07-01" && v["endDate"] == "2026-07-01"),
            default), Times.Once);
        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.AvailabilityExceptionDeclared,
            It.Is<Dictionary<string, string>>(v => v["startDate"] == "2026-07-02" && v["endDate"] == "2026-07-02"),
            default), Times.Once);
    }

    [Fact]
    public async Task AnnounceAsync_ContiguousDaysAvailableUntilGoesFromUnsetToSet_DoNotMerge()
    {
        // AvailableFrom matches (both null) so the chain reaches the AvailableUntil comparison,
        // where it flips from unset to set — the HasValue-mismatch branch for AvailableUntil.
        SetupResolve(Day1, Day2,
            before: [Resolved(Day1, DayAvailabilityStatus.Available), Resolved(Day2, DayAvailabilityStatus.Available)],
            after:
            [
                Resolved(Day1, DayAvailabilityStatus.Partial),
                Resolved(Day2, DayAvailabilityStatus.Partial, until: new TimeOnly(17, 0)),
            ]);

        await _sut.AnnounceAsync(MakeChange(Day1, Day2));

        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.AvailabilityExceptionDeclared,
            It.Is<Dictionary<string, string>>(v => v["startDate"] == "2026-07-01" && v["endDate"] == "2026-07-01"),
            default), Times.Once);
        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.AvailabilityExceptionDeclared,
            It.Is<Dictionary<string, string>>(v => v["startDate"] == "2026-07-02" && v["endDate"] == "2026-07-02"),
            default), Times.Once);
    }

    [Fact]
    public async Task AnnounceAsync_MultipleSegments_FetchesGuildLanguageOnlyOnce()
    {
        SetupResolve(Day1, Day3,
            before: [Resolved(Day1, DayAvailabilityStatus.Available), Resolved(Day2, DayAvailabilityStatus.Absent), Resolved(Day3, DayAvailabilityStatus.Available)],
            after: [Resolved(Day1, DayAvailabilityStatus.Absent), Resolved(Day2, DayAvailabilityStatus.Absent), Resolved(Day3, DayAvailabilityStatus.Absent)]);

        await _sut.AnnounceAsync(MakeChange(Day1, Day3));

        _contentBuilder.Verify(b => b.GetGuildLanguageAsync(GuildId, default), Times.Once);
    }

    // ── Global fan-out ──────────────────────────────────────────────────────

    [Fact]
    public async Task AnnounceAsync_Global_NoActiveBranches_NeverResolvesOrAnnounces()
    {
        await _sut.AnnounceAsync(MakeGlobalChange(Day1, Day1));

        _resolution.Verify(r => r.ResolveForScope(
            It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<IReadOnlyCollection<AvailabilityDeclaration>>(),
            It.IsAny<IReadOnlyCollection<RecurringAvailabilityPattern>>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        _auditLog.Verify(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GuildAuditAction>(), It.IsAny<Dictionary<string, string>>(), default), Times.Never);
        _dispatcher.Verify(d => d.NotifyAsync(It.IsAny<string>(), It.IsAny<GuildNotificationEventType>(), It.IsAny<int?>(), It.IsAny<DiscordEmbedContent>(), default), Times.Never);
    }

    [Fact]
    public async Task AnnounceAsync_Global_AnnouncesOncePerActiveBranchThatActuallyChanged()
    {
        // Branch A's resolved day actually flips (Available -> Absent) and should be announced;
        // branch B's is unchanged (Absent both before and after) and must produce no
        // audit/notify at all for that branch — this is the "skip branches without a visible
        // change" half of the fan-out contract, not just "loop over every active branch".
        _activeRosterResolver.Setup(r => r.GetActiveBranchesAsync(RequesterId, default))
            .ReturnsAsync([new ActiveRosterBranch("guild-a", 1), new ActiveRosterBranch("guild-b", 2)]);
        SetupResolveForScope("guild-a", 1, Day1, Day1,
            before: [Resolved(Day1, DayAvailabilityStatus.Available)],
            after: [Resolved(Day1, DayAvailabilityStatus.Absent)]);
        SetupResolveForScope("guild-b", 2, Day1, Day1,
            before: [Resolved(Day1, DayAvailabilityStatus.Absent)],
            after: [Resolved(Day1, DayAvailabilityStatus.Absent)]);

        await _sut.AnnounceAsync(MakeGlobalChange(Day1, Day1));

        _auditLog.Verify(a => a.LogAsync("guild-a", RequesterId, GuildAuditAction.AvailabilityExceptionDeclared, It.IsAny<Dictionary<string, string>>(), default), Times.Once);
        _auditLog.Verify(a => a.LogAsync("guild-b", It.IsAny<string>(), It.IsAny<GuildAuditAction>(), It.IsAny<Dictionary<string, string>>(), default), Times.Never);
        _dispatcher.Verify(d => d.NotifyAsync("guild-a", GuildNotificationEventType.AbsenceAdded, 1, It.IsAny<DiscordEmbedContent>(), default), Times.Once);
        _dispatcher.Verify(d => d.NotifyAsync("guild-b", It.IsAny<GuildNotificationEventType>(), It.IsAny<int?>(), It.IsAny<DiscordEmbedContent>(), default), Times.Never);
    }
}
