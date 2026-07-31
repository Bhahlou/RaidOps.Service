using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Events.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Events.CommandHandlers;

/// <summary>
/// Handles <see cref="PublishRaidEventCommand"/> by verifying officer access and marking the event
/// published — it becomes visible to non-officer roster members and starts counting toward the
/// "unassigned members" computation.
/// </summary>
public class PublishRaidEventCommandHandler(
    IGuildAccessService guildAccessService,
    IRaidEventRepository raidEventRepository,
    IAuditLogService auditLogService) : ICommandHandlerAsync<PublishRaidEventCommand>
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

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.RaidEventPublished,
            new Dictionary<string, string>
            {
                ["eventName"] = raidEvent.Name,
                ["startsAtUtc"] = raidEvent.StartsAtUtc.ToString("yyyy-MM-dd HH:mm"),
            },
            cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Raid event published successfully."));
    }
}
