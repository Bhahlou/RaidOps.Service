using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Events.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Events.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Raids;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Events.CommandHandlers;

public class UpdateRaidEventCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IRaidEventRepository> _raidEventRepository = new();
    private readonly Mock<IRaidZoneRepository> _raidZoneRepository = new();
    private readonly Mock<IGuildsRepository> _guildsRepository = new();
    private readonly Mock<IAuditLogService> _auditLogService = new();
    private readonly Mock<IGuildNotificationDispatcher> _guildNotificationDispatcher = new();
    private readonly Mock<IRaidNotificationContentBuilder> _raidNotificationContentBuilder = new();
    private readonly Mock<IRaidSignupAnnouncementService> _raidSignupAnnouncementService = new();
    private readonly Mock<IRaidCompositionAnnouncementService> _raidCompositionAnnouncementService = new();
    private readonly Mock<IDiscordBotService> _discordBotService = new();
    private readonly Mock<IGuildService> _guildService = new();
    private readonly Mock<ILogger<UpdateRaidEventCommandHandler>> _logger = new();
    private readonly UpdateRaidEventCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "officer-1";
    private const int EventId = 5;

    public UpdateRaidEventCommandHandlerTests()
    {
        _discordBotService.Setup(d => d.Guilds).Returns(_guildService.Object);
        _sut = new UpdateRaidEventCommandHandler(
            _access.Object, _raidEventRepository.Object, _raidZoneRepository.Object, _guildsRepository.Object, _auditLogService.Object,
            _guildNotificationDispatcher.Object, _raidNotificationContentBuilder.Object, _raidSignupAnnouncementService.Object,
            _raidCompositionAnnouncementService.Object, _discordBotService.Object, _logger.Object);
    }

    private static UpdateRaidEventCommand MakeCommand(
        int groupCount = 2, int slotsPerGroup = 5, List<int>? zoneIds = null,
        string? dedicatedAnnouncementChannelId = null, bool dedicatedAnnouncementChannelIsBotOwned = false) => new()
    {
        GuildId = GuildId,
        RequesterDiscordId = RequesterId,
        GuildBranchId = GuildBranchId,
        EventId = EventId,
        Name = "Split 1",
        StartsAtUtc = new DateTime(2026, 2, 1, 20, 0, 0, DateTimeKind.Utc),
        GroupCount = groupCount,
        SlotsPerGroup = slotsPerGroup,
        RaidZoneIds = zoneIds ?? [1],
        DedicatedAnnouncementChannelId = dedicatedAnnouncementChannelId,
        DedicatedAnnouncementChannelIsBotOwned = dedicatedAnnouncementChannelIsBotOwned,
    };

    /// <summary>Wires the successful-update happy path shared by every dedicated-channel test below.</summary>
    private void SetupSuccessfulUpdate(RaidEvent existing)
    {
        SetupOfficer();
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(existing);
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([new RaidZone { Id = 1 }]);
        _raidEventRepository.Setup(r => r.UpdateAsync(It.IsAny<RaidEvent>(), GuildBranchId, It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync(true);
    }

    private void SetupOfficer() =>
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);

    [Fact]
    public async Task HandleAsync_NotOfficer_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(2, 0)]
    public async Task HandleAsync_NonPositiveGridShape_ReturnsInvalidRequest(int groupCount, int slotsPerGroup)
    {
        SetupOfficer();

        var result = await _sut.HandleAsync(MakeCommand(groupCount, slotsPerGroup));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidRequest);
    }

    [Fact]
    public async Task HandleAsync_NoZonesTargeted_ReturnsInvalidRequest()
    {
        SetupOfficer();

        var result = await _sut.HandleAsync(MakeCommand(zoneIds: []));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidRequest);
    }

    [Fact]
    public async Task HandleAsync_EventNotFound_ReturnsRaidEventNotFound()
    {
        SetupOfficer();
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync((RaidEvent?)null);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidEventNotFound);
    }

    [Fact]
    public async Task HandleAsync_ShrinkingGridBelowExistingAssignment_ReturnsGridShrinkWouldOrphanAssignments()
    {
        SetupOfficer();
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent
        {
            Id = EventId,
            Assignments = [new RaidSlotAssignment { GroupNumber = 3, SlotNumber = 1 }],
        });

        // Shrinking to 2 groups would orphan the assignment sitting in group 3.
        var result = await _sut.HandleAsync(MakeCommand(groupCount: 2, slotsPerGroup: 5));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GridShrinkWouldOrphanAssignments);
        _raidZoneRepository.Verify(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ShrinkingSlotsPerGroupBelowExistingAssignment_ReturnsGridShrinkWouldOrphanAssignments()
    {
        SetupOfficer();
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent
        {
            Id = EventId,
            Assignments = [new RaidSlotAssignment { GroupNumber = 1, SlotNumber = 8 }],
        });

        var result = await _sut.HandleAsync(MakeCommand(groupCount: 2, slotsPerGroup: 5));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GridShrinkWouldOrphanAssignments);
    }

    [Fact]
    public async Task HandleAsync_UnknownZone_ReturnsRaidZoneNotFound()
    {
        SetupOfficer();
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent { Id = EventId, Assignments = [] });
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidZoneNotFound);
    }

    [Fact]
    public async Task HandleAsync_UpdateRaceLostBetweenReadAndWrite_ReturnsRaidEventNotFound()
    {
        SetupOfficer();
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent { Id = EventId, Assignments = [] });
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([new RaidZone { Id = 1 }]);
        _raidEventRepository.Setup(r => r.UpdateAsync(It.IsAny<RaidEvent>(), GuildBranchId, It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync(false);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidEventNotFound);
        _auditLogService.Verify(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GuildAuditAction>(), It.IsAny<Dictionary<string, string>>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Success_UpdatesAndLogsAudit()
    {
        SetupOfficer();
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent { Id = EventId, Assignments = [] });
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([new RaidZone { Id = 1 }]);
        _raidEventRepository.Setup(r => r.UpdateAsync(It.IsAny<RaidEvent>(), GuildBranchId, It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync(true);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _raidEventRepository.Verify(r => r.UpdateAsync(
            It.Is<RaidEvent>(e => e.Id == EventId && e.Name == "Split 1" && e.GroupCount == 2 && e.SlotsPerGroup == 5),
            GuildBranchId, It.Is<IEnumerable<int>>(ids => ids.Single() == 1), default), Times.Once);
        _auditLogService.Verify(a => a.LogAsync(GuildId, RequesterId, GuildAuditAction.RaidEventUpdated, It.IsAny<Dictionary<string, string>>(), default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Success_LogsAuditWithGuildLocalTimeDiffAndZoneNameDiff()
    {
        SetupOfficer();
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent
        {
            Id = EventId,
            // MakeCommand's fixed StartsAtUtc is 2026-02-01 20:00 UTC — pick a distinctly different
            // old value so old/new actually differ in the audit diff.
            StartsAtUtc = new DateTime(2026, 1, 15, 18, 0, 0, DateTimeKind.Utc),
            Assignments = [],
            TargetZones = [new RaidEventZone { RaidZone = new RaidZone { Id = 1, Name = "Molten Core" } }],
        });
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([new RaidZone { Id = 2, Name = "Blackwing Lair" }]);
        _raidEventRepository.Setup(r => r.UpdateAsync(It.IsAny<RaidEvent>(), GuildBranchId, It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync(true);
        _guildsRepository.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync(new Guild { Id = GuildId, Name = "G", Timezone = "Europe/Paris" });

        var result = await _sut.HandleAsync(MakeCommand(zoneIds: [2]));

        result.IsSuccess.Should().BeTrue();
        _auditLogService.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.RaidEventUpdated,
            It.Is<Dictionary<string, string>>(d =>
                d["eventName"] == "Split 1" &&
                d["oldStartsAtLocal"] == "2026-01-15 19:00" &&
                d["newStartsAtLocal"] == "2026-02-01 21:00" &&
                d["oldRaidZoneNames"] == "Molten Core" &&
                d["newRaidZoneNames"] == "Blackwing Lair"),
            default), Times.Once);
    }

    // ── Notification gating: published + start time actually changed only ───

    [Fact]
    public async Task HandleAsync_DraftEventWithTimeChange_DoesNotNotify()
    {
        SetupOfficer();
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent
        {
            Id = EventId,
            PublicationStatus = RaidPublicationStatus.Draft,
            StartsAtUtc = new DateTime(2026, 1, 1, 20, 0, 0, DateTimeKind.Utc),
            Assignments = [],
        });
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([new RaidZone { Id = 1 }]);
        _raidEventRepository.Setup(r => r.UpdateAsync(It.IsAny<RaidEvent>(), GuildBranchId, It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync(true);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _guildNotificationDispatcher.Verify(d => d.NotifyAsync(
            It.IsAny<string>(), It.IsAny<GuildNotificationEventType>(), It.IsAny<int?>(), It.IsAny<DiscordEmbedContent>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PublishedEventWithoutTimeChange_DoesNotNotify()
    {
        SetupOfficer();
        var sameStartsAtUtc = new DateTime(2026, 2, 1, 20, 0, 0, DateTimeKind.Utc);
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent
        {
            Id = EventId,
            PublicationStatus = RaidPublicationStatus.Published,
            StartsAtUtc = sameStartsAtUtc,
            Assignments = [],
        });
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([new RaidZone { Id = 1 }]);
        _raidEventRepository.Setup(r => r.UpdateAsync(It.IsAny<RaidEvent>(), GuildBranchId, It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync(true);

        // MakeCommand's own StartsAtUtc is identical to sameStartsAtUtc — same instant, no reschedule.
        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _guildNotificationDispatcher.Verify(d => d.NotifyAsync(
            It.IsAny<string>(), It.IsAny<GuildNotificationEventType>(), It.IsAny<int?>(), It.IsAny<DiscordEmbedContent>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PublishedEventWithTimeChange_BuildsAndDispatchesRescheduledNotification()
    {
        SetupOfficer();
        var oldStartsAtUtc = new DateTime(2026, 1, 1, 20, 0, 0, DateTimeKind.Utc);
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent
        {
            Id = EventId,
            PublicationStatus = RaidPublicationStatus.Published,
            StartsAtUtc = oldStartsAtUtc,
            Assignments = [],
        });
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([new RaidZone { Id = 1 }]);
        _raidEventRepository.Setup(r => r.UpdateAsync(It.IsAny<RaidEvent>(), GuildBranchId, It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync(true);
        var embed = new DiscordEmbedContent("Raid rescheduled");
        _raidNotificationContentBuilder
            .Setup(b => b.BuildRescheduledAsync(GuildId, RequesterId, It.Is<RaidEvent>(e => e.Id == EventId), oldStartsAtUtc, default))
            .ReturnsAsync(embed);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _guildNotificationDispatcher.Verify(d => d.NotifyAsync(GuildId, GuildNotificationEventType.RaidRescheduled, GuildBranchId, embed, default), Times.Once);
    }

    // ── Dedicated channel move ──────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_ChannelUnchanged_DoesNotMoveAnything()
    {
        SetupSuccessfulUpdate(new RaidEvent { Id = EventId, DedicatedAnnouncementChannelId = "111", DedicatedAnnouncementChannelIsBotOwned = true });

        var result = await _sut.HandleAsync(MakeCommand(dedicatedAnnouncementChannelId: "111", dedicatedAnnouncementChannelIsBotOwned: true));

        result.IsSuccess.Should().BeTrue();
        _raidSignupAnnouncementService.Verify(s => s.DeleteSignupCallAsync(It.IsAny<RaidEvent>(), default), Times.Never);
        _raidCompositionAnnouncementService.Verify(s => s.DeleteAnnouncementAsync(It.IsAny<RaidEvent>(), default), Times.Never);
        _raidEventRepository.Verify(r => r.ClearAnnouncementReferencesAsync(It.IsAny<int>(), It.IsAny<int>(), default), Times.Never);
        _guildService.Verify(g => g.DeleteChannelAsync(It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ChannelChanged_DropsOldEmbedsAndClearsReferences()
    {
        var existing = new RaidEvent { Id = EventId, DedicatedAnnouncementChannelId = "111", DedicatedAnnouncementChannelIsBotOwned = false };
        SetupSuccessfulUpdate(existing);

        var result = await _sut.HandleAsync(MakeCommand(dedicatedAnnouncementChannelId: "222"));

        result.IsSuccess.Should().BeTrue();
        _raidSignupAnnouncementService.Verify(s => s.DeleteSignupCallAsync(existing, default), Times.Once);
        _raidCompositionAnnouncementService.Verify(s => s.DeleteAnnouncementAsync(existing, default), Times.Once);
        _raidEventRepository.Verify(r => r.ClearAnnouncementReferencesAsync(EventId, GuildBranchId, default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ChannelChangedAndOldWasBotOwned_DeletesOldChannel()
    {
        var existing = new RaidEvent { Id = EventId, DedicatedAnnouncementChannelId = "111", DedicatedAnnouncementChannelIsBotOwned = true };
        SetupSuccessfulUpdate(existing);

        var result = await _sut.HandleAsync(MakeCommand(dedicatedAnnouncementChannelId: "222"));

        result.IsSuccess.Should().BeTrue();
        _guildService.Verify(g => g.DeleteChannelAsync("111", default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ChannelChangedButOldWasNotBotOwned_NeverDeletesOldChannel()
    {
        var existing = new RaidEvent { Id = EventId, DedicatedAnnouncementChannelId = "111", DedicatedAnnouncementChannelIsBotOwned = false };
        SetupSuccessfulUpdate(existing);

        var result = await _sut.HandleAsync(MakeCommand(dedicatedAnnouncementChannelId: "222"));

        result.IsSuccess.Should().BeTrue();
        _guildService.Verify(g => g.DeleteChannelAsync(It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ChannelChangedOldChannelDeleteThrows_StillSucceeds()
    {
        var existing = new RaidEvent { Id = EventId, DedicatedAnnouncementChannelId = "111", DedicatedAnnouncementChannelIsBotOwned = true };
        SetupSuccessfulUpdate(existing);
        _guildService.Setup(g => g.DeleteChannelAsync("111", default)).ThrowsAsync(new InvalidOperationException("gone"));

        var result = await _sut.HandleAsync(MakeCommand(dedicatedAnnouncementChannelId: "222"));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_ChannelChangedAndSignupMode_RepostsSignupCallInNewChannel()
    {
        var existing = new RaidEvent { Id = EventId, SignupMode = SignupMode.Signup, DedicatedAnnouncementChannelId = "111" };
        SetupSuccessfulUpdate(existing);

        var result = await _sut.HandleAsync(MakeCommand(dedicatedAnnouncementChannelId: "222"));

        result.IsSuccess.Should().BeTrue();
        _raidSignupAnnouncementService.Verify(s => s.PublishOrUpdateSignupCallAsync(It.Is<RaidEvent>(e => e.Id == EventId), default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ChannelChangedAndNotSignupMode_NeverRepostsSignupCall()
    {
        var existing = new RaidEvent { Id = EventId, SignupMode = SignupMode.DefaultPresent, DedicatedAnnouncementChannelId = "111" };
        SetupSuccessfulUpdate(existing);

        var result = await _sut.HandleAsync(MakeCommand(dedicatedAnnouncementChannelId: "222"));

        result.IsSuccess.Should().BeTrue();
        _raidSignupAnnouncementService.Verify(s => s.PublishOrUpdateSignupCallAsync(It.IsAny<RaidEvent>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ChannelIdSetAndBotOwnedTrue_PersistsBotOwnedTrue()
    {
        SetupSuccessfulUpdate(new RaidEvent { Id = EventId });

        var result = await _sut.HandleAsync(MakeCommand(dedicatedAnnouncementChannelId: "222", dedicatedAnnouncementChannelIsBotOwned: true));

        result.IsSuccess.Should().BeTrue();
        _raidEventRepository.Verify(r => r.UpdateAsync(
            It.Is<RaidEvent>(e => e.DedicatedAnnouncementChannelId == "222" && e.DedicatedAnnouncementChannelIsBotOwned),
            GuildBranchId, It.IsAny<IEnumerable<int>>(), default), Times.Once);
    }
}
