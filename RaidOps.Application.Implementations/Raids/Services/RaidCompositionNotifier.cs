using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Helpers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Services;

/// <inheritdoc cref="IRaidCompositionNotifier"/>
public class RaidCompositionNotifier(
    IGuildsRepository guildsRepository,
    IAuditLogService auditLogService,
    IGuildNotificationDispatcher guildNotificationDispatcher,
    IRaidNotificationContentBuilder raidNotificationContentBuilder,
    IRaidCompositionAnnouncementService raidCompositionAnnouncementService) : IRaidCompositionNotifier
{
    private const string EventNameKey = "eventName";
    private const string StartsAtLocalKey = "startsAtLocal";

    /// <inheritdoc/>
    public async Task NotifySlotAssignedAsync(RaidEvent raidEvent, string requesterDiscordId, RaidCharacterRef character, string playerDiscordId, SlotCoordinate slot, CancellationToken cancellationToken = default)
    {
        var startsAtLocal = await ResolveStartsAtLocalAsync(raidEvent, cancellationToken);

        var variables = new Dictionary<string, string>
        {
            [EventNameKey] = raidEvent.Name,
            [StartsAtLocalKey] = startsAtLocal,
            ["characterName"] = character.Name,
            ["groupNumber"] = slot.GroupNumber.ToString(),
            ["slotNumber"] = slot.SlotNumber.ToString(),
        };
        if (character.ClassId is { } classId)
            variables["characterClassId"] = classId.ToString();

        await auditLogService.LogAsync(raidEvent.GuildId, requesterDiscordId, GuildAuditAction.SlotAssigned, variables, cancellationToken);

        var embed = await raidNotificationContentBuilder.BuildSlotAssignedAsync(raidEvent.GuildId, requesterDiscordId, raidEvent, character, slot, cancellationToken);
        await guildNotificationDispatcher.NotifyAsync(raidEvent.GuildId, GuildNotificationEventType.RaidSlotAssigned, raidEvent.GuildBranchId, embed, cancellationToken);

        await raidCompositionAnnouncementService.PublishOrUpdateAnnouncementAsync(raidEvent, cancellationToken);
        await raidCompositionAnnouncementService.NotifyPlayerAddedAsync(raidEvent, playerDiscordId, character, isInitialPublish: false, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task NotifySlotUnassignedAsync(RaidEvent raidEvent, string requesterDiscordId, RaidCharacterRef character, string playerDiscordId, SlotCoordinate slot, CancellationToken cancellationToken = default)
    {
        var startsAtLocal = await ResolveStartsAtLocalAsync(raidEvent, cancellationToken);

        var variables = new Dictionary<string, string>
        {
            [EventNameKey] = raidEvent.Name,
            [StartsAtLocalKey] = startsAtLocal,
            ["characterName"] = character.Name,
            ["groupNumber"] = slot.GroupNumber.ToString(),
            ["slotNumber"] = slot.SlotNumber.ToString(),
        };
        if (character.ClassId is { } classId)
            variables["characterClassId"] = classId.ToString();

        await auditLogService.LogAsync(raidEvent.GuildId, requesterDiscordId, GuildAuditAction.SlotUnassigned, variables, cancellationToken);

        var embed = await raidNotificationContentBuilder.BuildSlotUnassignedAsync(raidEvent.GuildId, requesterDiscordId, raidEvent, character, slot, cancellationToken);
        await guildNotificationDispatcher.NotifyAsync(raidEvent.GuildId, GuildNotificationEventType.RaidSlotUnassigned, raidEvent.GuildBranchId, embed, cancellationToken);

        await raidCompositionAnnouncementService.PublishOrUpdateAnnouncementAsync(raidEvent, cancellationToken);
        await raidCompositionAnnouncementService.NotifyPlayerRemovedAsync(raidEvent, playerDiscordId, character, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task NotifySlotsSwappedAsync(RaidEvent raidEvent, string requesterDiscordId, RaidCharacterRef characterA, SlotCoordinate slotA, RaidCharacterRef characterB, SlotCoordinate slotB, CancellationToken cancellationToken = default)
    {
        var startsAtLocal = await ResolveStartsAtLocalAsync(raidEvent, cancellationToken);

        var variables = new Dictionary<string, string>
        {
            [EventNameKey] = raidEvent.Name,
            [StartsAtLocalKey] = startsAtLocal,
            ["characterAName"] = characterA.Name,
            ["groupNumberA"] = slotA.GroupNumber.ToString(),
            ["slotNumberA"] = slotA.SlotNumber.ToString(),
            ["characterBName"] = characterB.Name,
            ["groupNumberB"] = slotB.GroupNumber.ToString(),
            ["slotNumberB"] = slotB.SlotNumber.ToString(),
        };
        if (characterA.ClassId is { } classIdA)
            variables["characterAClassId"] = classIdA.ToString();
        if (characterB.ClassId is { } classIdB)
            variables["characterBClassId"] = classIdB.ToString();

        await auditLogService.LogAsync(raidEvent.GuildId, requesterDiscordId, GuildAuditAction.SlotsSwapped, variables, cancellationToken);

        var embed = await raidNotificationContentBuilder.BuildSlotsSwappedAsync(raidEvent.GuildId, requesterDiscordId, raidEvent, characterA, characterB, cancellationToken);
        await guildNotificationDispatcher.NotifyAsync(raidEvent.GuildId, GuildNotificationEventType.RaidSlotsSwapped, raidEvent.GuildBranchId, embed, cancellationToken);

        // No DM here — a swap changes nobody's presence, both players stay in the raid, just at a different slot.
        await raidCompositionAnnouncementService.PublishOrUpdateAnnouncementAsync(raidEvent, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task NotifySlotSpecChangedAsync(RaidEvent raidEvent, string requesterDiscordId, RaidCharacterRef character, string playerDiscordId, string oldSpecName, string newSpecName, CancellationToken cancellationToken = default)
    {
        var startsAtLocal = await ResolveStartsAtLocalAsync(raidEvent, cancellationToken);

        var variables = new Dictionary<string, string>
        {
            [EventNameKey] = raidEvent.Name,
            [StartsAtLocalKey] = startsAtLocal,
            ["characterName"] = character.Name,
            ["oldSpecName"] = oldSpecName,
            ["newSpecName"] = newSpecName,
        };
        if (character.ClassId is { } classId)
            variables["characterClassId"] = classId.ToString();

        await auditLogService.LogAsync(raidEvent.GuildId, requesterDiscordId, GuildAuditAction.SlotAssignmentSpecChanged, variables, cancellationToken);

        var embed = await raidNotificationContentBuilder.BuildSlotSpecChangedAsync(raidEvent.GuildId, requesterDiscordId, raidEvent, character, oldSpecName, newSpecName, cancellationToken);
        await guildNotificationDispatcher.NotifyAsync(raidEvent.GuildId, GuildNotificationEventType.RaidSlotSpecChanged, raidEvent.GuildBranchId, embed, cancellationToken);

        await raidCompositionAnnouncementService.PublishOrUpdateAnnouncementAsync(raidEvent, cancellationToken);
        await raidCompositionAnnouncementService.NotifyPlayerSpecChangedAsync(raidEvent, playerDiscordId, character, oldSpecName, newSpecName, cancellationToken);
    }

    private async Task<string> ResolveStartsAtLocalAsync(RaidEvent raidEvent, CancellationToken cancellationToken)
    {
        var guild = await guildsRepository.GetByIdAsync(raidEvent.GuildId, cancellationToken);
        return GuildTimeHelper.ToGuildLocalDateTime(raidEvent.StartsAtUtc, guild?.Timezone).ToString("yyyy-MM-dd HH:mm");
    }
}
