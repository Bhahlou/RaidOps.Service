using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Raids;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Services;

public class RaidCompositionNotifierTests
{
    private readonly Mock<IGuildsRepository> _guildsRepository = new();
    private readonly Mock<IAuditLogService> _auditLogService = new();
    private readonly Mock<IGuildNotificationDispatcher> _guildNotificationDispatcher = new();
    private readonly Mock<IRaidNotificationContentBuilder> _raidNotificationContentBuilder = new();
    private readonly Mock<IRaidCompositionAnnouncementService> _raidCompositionAnnouncementService = new();
    private readonly RaidCompositionNotifier _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "officer-1";
    private const string PlayerDiscordId = "player-1";

    public RaidCompositionNotifierTests()
    {
        _sut = new RaidCompositionNotifier(
            _guildsRepository.Object, _auditLogService.Object, _guildNotificationDispatcher.Object,
            _raidNotificationContentBuilder.Object, _raidCompositionAnnouncementService.Object);

        _guildsRepository.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync(new Guild { Id = GuildId, Name = "G", Timezone = "Europe/Paris" });
    }

    private static RaidEvent MakeEvent() => new()
    {
        Id = 5,
        GuildId = GuildId,
        GuildBranchId = GuildBranchId,
        Name = "Split 1",
        StartsAtUtc = new DateTime(2026, 2, 1, 20, 0, 0, DateTimeKind.Utc),
    };

    // ── NotifySlotAssignedAsync ───────────────────────────────────────────────

    [Fact]
    public async Task NotifySlotAssignedAsync_WithClassId_LogsAuditAndDispatchesEmbed()
    {
        var raidEvent = MakeEvent();
        var character = new RaidCharacterRef("Arthas", 6, "Blood");
        var slot = new SlotCoordinate(2, 3);
        var embed = new DiscordEmbedContent("Character added");
        _raidNotificationContentBuilder.Setup(b => b.BuildSlotAssignedAsync(GuildId, RequesterId, raidEvent, character, slot, default)).ReturnsAsync(embed);

        await _sut.NotifySlotAssignedAsync(raidEvent, RequesterId, character, PlayerDiscordId, slot);

        _auditLogService.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.SlotAssigned,
            It.Is<Dictionary<string, string>>(d =>
                d["eventName"] == "Split 1" &&
                d["startsAtLocal"] == "2026-02-01 21:00" &&
                d["characterName"] == "Arthas" &&
                d["groupNumber"] == "2" &&
                d["slotNumber"] == "3" &&
                d["characterClassId"] == "6"),
            default), Times.Once);
        _guildNotificationDispatcher.Verify(d => d.NotifyAsync(GuildId, GuildNotificationEventType.RaidSlotAssigned, GuildBranchId, embed, default), Times.Once);
    }

    [Fact]
    public async Task NotifySlotAssignedAsync_NoClassId_OmitsCharacterClassIdVariable()
    {
        var raidEvent = MakeEvent();
        var character = new RaidCharacterRef("Unknown", null);
        var slot = new SlotCoordinate(1, 1);
        _raidNotificationContentBuilder.Setup(b => b.BuildSlotAssignedAsync(GuildId, RequesterId, raidEvent, character, slot, default)).ReturnsAsync(new DiscordEmbedContent("x"));

        await _sut.NotifySlotAssignedAsync(raidEvent, RequesterId, character, PlayerDiscordId, slot);

        _auditLogService.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.SlotAssigned,
            It.Is<Dictionary<string, string>>(d => !d.ContainsKey("characterClassId")),
            default), Times.Once);
    }

    // ── NotifySlotUnassignedAsync ─────────────────────────────────────────────

    [Fact]
    public async Task NotifySlotUnassignedAsync_LogsAuditAndDispatchesEmbed()
    {
        var raidEvent = MakeEvent();
        var character = new RaidCharacterRef("Jaina", 8, "Frost");
        var slot = new SlotCoordinate(4, 5);
        var embed = new DiscordEmbedContent("Character removed");
        _raidNotificationContentBuilder.Setup(b => b.BuildSlotUnassignedAsync(GuildId, RequesterId, raidEvent, character, slot, default)).ReturnsAsync(embed);

        await _sut.NotifySlotUnassignedAsync(raidEvent, RequesterId, character, PlayerDiscordId, slot);

        _auditLogService.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.SlotUnassigned,
            It.Is<Dictionary<string, string>>(d =>
                d["characterName"] == "Jaina" && d["groupNumber"] == "4" && d["slotNumber"] == "5" && d["characterClassId"] == "8"),
            default), Times.Once);
        _guildNotificationDispatcher.Verify(d => d.NotifyAsync(GuildId, GuildNotificationEventType.RaidSlotUnassigned, GuildBranchId, embed, default), Times.Once);
    }

    // ── NotifySlotsSwappedAsync ───────────────────────────────────────────────

    [Fact]
    public async Task NotifySlotsSwappedAsync_LogsAuditWithBothCharactersAndDispatchesEmbed()
    {
        var raidEvent = MakeEvent();
        var characterA = new RaidCharacterRef("Arthas", 6, "Blood");
        var characterB = new RaidCharacterRef("Jaina", 8, "Frost");
        var slotA = new SlotCoordinate(1, 1);
        var slotB = new SlotCoordinate(2, 2);
        var embed = new DiscordEmbedContent("Characters swapped");
        _raidNotificationContentBuilder.Setup(b => b.BuildSlotsSwappedAsync(GuildId, RequesterId, raidEvent, characterA, characterB, default)).ReturnsAsync(embed);

        await _sut.NotifySlotsSwappedAsync(raidEvent, RequesterId, characterA, slotA, characterB, slotB);

        _auditLogService.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.SlotsSwapped,
            It.Is<Dictionary<string, string>>(d =>
                d["characterAName"] == "Arthas" && d["groupNumberA"] == "1" && d["slotNumberA"] == "1" && d["characterAClassId"] == "6" &&
                d["characterBName"] == "Jaina" && d["groupNumberB"] == "2" && d["slotNumberB"] == "2" && d["characterBClassId"] == "8"),
            default), Times.Once);
        _guildNotificationDispatcher.Verify(d => d.NotifyAsync(GuildId, GuildNotificationEventType.RaidSlotsSwapped, GuildBranchId, embed, default), Times.Once);
    }

    // ── NotifySlotSpecChangedAsync ────────────────────────────────────────────

    [Fact]
    public async Task NotifySlotSpecChangedAsync_LogsAuditWithOldAndNewSpecAndDispatchesEmbed()
    {
        var raidEvent = MakeEvent();
        var character = new RaidCharacterRef("Arthas", 6);
        var embed = new DiscordEmbedContent("Slot spec changed");
        _raidNotificationContentBuilder.Setup(b => b.BuildSlotSpecChangedAsync(GuildId, RequesterId, raidEvent, character, "Arms", "Fury", default)).ReturnsAsync(embed);

        await _sut.NotifySlotSpecChangedAsync(raidEvent, RequesterId, character, PlayerDiscordId, "Arms", "Fury");

        _auditLogService.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.SlotAssignmentSpecChanged,
            It.Is<Dictionary<string, string>>(d => d["characterName"] == "Arthas" && d["oldSpecName"] == "Arms" && d["newSpecName"] == "Fury" && d["characterClassId"] == "6"),
            default), Times.Once);
        _guildNotificationDispatcher.Verify(d => d.NotifyAsync(GuildId, GuildNotificationEventType.RaidSlotSpecChanged, GuildBranchId, embed, default), Times.Once);
    }

    // ── Guild timezone resolution ─────────────────────────────────────────────

    [Fact]
    public async Task NotifySlotAssignedAsync_GuildNotFound_FallsBackToUtcForStartsAtLocal()
    {
        _guildsRepository.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync((Guild?)null);
        var raidEvent = MakeEvent();
        var character = new RaidCharacterRef("Arthas", 6);
        var slot = new SlotCoordinate(1, 1);
        _raidNotificationContentBuilder.Setup(b => b.BuildSlotAssignedAsync(GuildId, RequesterId, raidEvent, character, slot, default)).ReturnsAsync(new DiscordEmbedContent("x"));

        await _sut.NotifySlotAssignedAsync(raidEvent, RequesterId, character, PlayerDiscordId, slot);

        _auditLogService.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.SlotAssigned,
            It.Is<Dictionary<string, string>>(d => d["startsAtLocal"] == "2026-02-01 20:00"),
            default), Times.Once);
    }
}
