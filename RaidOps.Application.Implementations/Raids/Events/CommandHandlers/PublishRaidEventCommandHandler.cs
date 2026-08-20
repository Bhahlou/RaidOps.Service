using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Events.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Helpers;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Events.CommandHandlers;

/// <summary>
/// Handles <see cref="PublishRaidEventCommand"/> by verifying officer access and marking the event
/// published — it becomes visible to non-officer roster members and starts counting toward the
/// "unassigned members" computation. Also posts a "Raid published" Discord notification, always via
/// the guild-wide configured channel — unlike composition/signup-call/grouping-ping, this
/// notification is deliberately NOT redirected to a raid's dedicated announcement channel even when
/// one is set, since it's a generic "a raid exists" ping, not raid-specific content.
/// </summary>
public class PublishRaidEventCommandHandler(
    IGuildAccessService guildAccessService,
    IRaidEventRepository raidEventRepository,
    IGuildsRepository guildsRepository,
    IAuditLogService auditLogService,
    IGuildNotificationDispatcher guildNotificationDispatcher,
    IRaidNotificationContentBuilder raidNotificationContentBuilder,
    IRaidCompositionAnnouncementService raidCompositionAnnouncementService) : ICommandHandlerAsync<PublishRaidEventCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(PublishRaidEventCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, command.GuildBranchId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild branch.");

        var raidEvent = await raidEventRepository.GetByIdAsync(command.EventId, command.GuildBranchId, cancellationToken);
        if (raidEvent == null)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidEventNotFound, $"Raid event '{command.EventId}' does not exist.");

        if (raidEvent.PublicationStatus == RaidPublicationStatus.Published)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidEventAlreadyPublished, "Raid event is already published.");

        var published = await raidEventRepository.PublishAsync(command.EventId, command.GuildBranchId, command.RequesterDiscordId, cancellationToken);
        if (!published)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidEventNotFound, $"Raid event '{command.EventId}' does not exist.");

        var guild = await guildsRepository.GetByIdAsync(command.GuildId, cancellationToken);
        var startsAtLocal = GuildTimeHelper.ToGuildLocalDateTime(raidEvent.StartsAtUtc, guild?.Timezone);

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.RaidEventPublished,
            new Dictionary<string, string>
            {
                ["eventName"] = raidEvent.Name,
                ["startsAtLocal"] = startsAtLocal.ToString("yyyy-MM-dd HH:mm"),
                ["raidZoneNames"] = string.Join(", ", raidEvent.TargetZones.Select(z => z.RaidZone.Name)),
            },
            cancellationToken);

        var embed = await raidNotificationContentBuilder.BuildPublishedAsync(command.GuildId, command.RequesterDiscordId, raidEvent, cancellationToken);
        await guildNotificationDispatcher.NotifyAsync(command.GuildId, GuildNotificationEventType.RaidPublished, command.GuildBranchId, embed, cancellationToken);

        await raidCompositionAnnouncementService.PublishOrUpdateAnnouncementAsync(raidEvent, cancellationToken);

        // The public embed never pings anyone (it's edited in place on every future change, so
        // pinging would spam on every roster edit) — DM every already-assigned player now, since
        // this publish is otherwise the only moment they'd learn they're in the raid.
        foreach (var assignment in raidEvent.Assignments)
        {
            var character = new RaidCharacterRef(assignment.Character.Name, assignment.Character.ClassId, assignment.Spec.Name);
            await raidCompositionAnnouncementService.NotifyPlayerAddedAsync(raidEvent, assignment.AssignedPlayerDiscordId, character, isInitialPublish: true, cancellationToken);
        }

        return Result<CommandResponse>.Ok(new CommandResponse("Raid event published successfully."));
    }
}
