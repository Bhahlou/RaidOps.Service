using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Events.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Helpers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Events.CommandHandlers;

/// <summary>
/// Handles <see cref="UpdateRaidEventCommand"/> by verifying officer access, validating the
/// requested grid shape and target zones, then replacing the event's schedule and target-zone
/// set. Works for both ad-hoc events and series-materialized occurrences — never mutates a
/// parent series. Shrinking <see cref="UpdateRaidEventCommand.GroupCount"/>/<see cref="UpdateRaidEventCommand.SlotsPerGroup"/>
/// below a coordinate that already has an assignment is rejected outright — the grid would
/// otherwise keep an assignment the UI can never again render or unassign (no slot to click), and
/// role counters would keep counting a character nobody can see anymore. If the event is already
/// published and the start time actually changes, also posts a "Raid rescheduled" Discord
/// notification — other field changes (grid size, zones, name) never trigger one. If the dedicated
/// channel actually changes, the standing composition/signup-call embeds are dropped from the old
/// channel, their references cleared, the signup-call embed re-posted fresh in the new channel
/// (Signup-mode events only), and the old channel itself deleted if RaidOps had created it just for
/// this event — see <see cref="Domain.Models.Raids.RaidEvent.DedicatedAnnouncementChannelIsBotOwned"/>.
/// Changing <see cref="UpdateRaidEventCommand.ExtendsRaidEventId"/> re-normalizes to the target's
/// chain root exactly like creation, and — if this event was itself a chain's root with other events
/// pointing at it — re-points those over to the new root so the flattening invariant survives the edit.
/// </summary>
public class UpdateRaidEventCommandHandler(
    IGuildAccessService guildAccessService,
    IRaidEventRepository raidEventRepository,
    IRaidZoneRepository raidZoneRepository,
    IGuildsRepository guildsRepository,
    IAuditLogService auditLogService,
    IRaidEventUpdateNotifier raidEventUpdateNotifier) : ICommandHandlerAsync<UpdateRaidEventCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(UpdateRaidEventCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, command.GuildBranchId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild branch.");

        if (command.GroupCount <= 0 || command.SlotsPerGroup <= 0)
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, "GroupCount and SlotsPerGroup must be positive.");

        var distinctZoneIds = command.RaidZoneIds.Distinct().ToList();
        if (distinctZoneIds.Count == 0)
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, "At least one raid zone must be targeted.");

        var existing = await raidEventRepository.GetByIdAsync(command.EventId, command.GuildBranchId, cancellationToken);
        if (existing == null)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidEventNotFound, $"Raid event '{command.EventId}' does not exist.");

        if (existing.Assignments.Any(a => a.GroupNumber > command.GroupCount || a.SlotNumber > command.SlotsPerGroup))
            return Result<CommandResponse>.Fail(ResponseDetail.GridShrinkWouldOrphanAssignments, "Unassign every character outside the new grid size first.");

        var zones = await raidZoneRepository.GetByIdsAsync(distinctZoneIds, cancellationToken);
        if (zones.Count != distinctZoneIds.Count)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidZoneNotFound, "One or more raid zones do not exist.");

        int? extendsRaidEventId = null;
        if (command.ExtendsRaidEventId is { } requestedExtendsId)
        {
            if (requestedExtendsId == command.EventId)
                return Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, "A raid event cannot extend itself.");

            var extendsTarget = await raidEventRepository.GetByIdAsync(requestedExtendsId, command.GuildBranchId, cancellationToken);
            if (extendsTarget == null)
                return Result<CommandResponse>.Fail(ResponseDetail.RaidEventNotFound, $"Raid event '{requestedExtendsId}' does not exist on this guild branch.");

            // Normalized to the chain's root, same as creation — see CreateAdhocRaidEventCommandHandler.
            extendsRaidEventId = extendsTarget.ExtendsRaidEventId ?? extendsTarget.Id;

            if (extendsRaidEventId == command.EventId)
                return Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, "This raid event is already part of that extension chain — it can't extend it too.");
        }

        var channelChanged = command.DedicatedAnnouncementChannelId != existing.DedicatedAnnouncementChannelId;

        var raidEvent = new RaidEvent
        {
            Id = command.EventId,
            Name = command.Name,
            StartsAtUtc = command.StartsAtUtc,
            GroupCount = command.GroupCount,
            SlotsPerGroup = command.SlotsPerGroup,
            ExtendsRaidEventId = extendsRaidEventId,
            DedicatedAnnouncementChannelId = command.DedicatedAnnouncementChannelId,
            DedicatedAnnouncementChannelIsBotOwned = command.DedicatedAnnouncementChannelId is not null && command.DedicatedAnnouncementChannelIsBotOwned,
            UpdatedAt = DateTime.UtcNow,
        };

        var updated = await raidEventRepository.UpdateAsync(raidEvent, command.GuildBranchId, distinctZoneIds, cancellationToken);
        if (!updated)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidEventNotFound, $"Raid event '{command.EventId}' does not exist.");

        // If this event was itself an extension chain's root (other events pointing at it), keep the
        // flattening invariant intact by re-pointing them straight at wherever this event now points.
        if (extendsRaidEventId != existing.ExtendsRaidEventId)
            await raidEventRepository.RepointExtensionChainAsync(command.EventId, extendsRaidEventId, command.GuildBranchId, cancellationToken);

        if (channelChanged)
            await raidEventUpdateNotifier.MoveDedicatedChannelAsync(command.EventId, command.GuildBranchId, existing, cancellationToken);

        var guild = await guildsRepository.GetByIdAsync(command.GuildId, cancellationToken);
        var oldStartsAtLocal = GuildTimeHelper.ToGuildLocalDateTime(existing.StartsAtUtc, guild?.Timezone);
        var newStartsAtLocal = GuildTimeHelper.ToGuildLocalDateTime(command.StartsAtUtc, guild?.Timezone);

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.RaidEventUpdated,
            new Dictionary<string, string>
            {
                ["eventName"] = command.Name,
                ["oldStartsAtLocal"] = oldStartsAtLocal.ToString("yyyy-MM-dd HH:mm"),
                ["newStartsAtLocal"] = newStartsAtLocal.ToString("yyyy-MM-dd HH:mm"),
                ["oldRaidZoneNames"] = string.Join(", ", existing.TargetZones.Select(z => z.RaidZone.Name)),
                ["newRaidZoneNames"] = string.Join(", ", zones.Select(z => z.Name)),
            },
            cancellationToken);

        if (existing.PublicationStatus == RaidPublicationStatus.Published && command.StartsAtUtc != existing.StartsAtUtc)
            await raidEventUpdateNotifier.NotifyRescheduledAsync(command.GuildId, command.RequesterDiscordId, command.GuildBranchId, raidEvent, existing.StartsAtUtc, cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Raid event updated successfully."));
    }
}
