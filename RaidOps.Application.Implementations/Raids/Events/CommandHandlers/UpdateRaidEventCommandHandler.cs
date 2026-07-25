using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Events.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Events.CommandHandlers;

/// <summary>
/// Handles <see cref="UpdateRaidEventCommand"/> by verifying officer access, validating the
/// requested grid shape and target zones, then replacing the event's schedule and target-zone
/// set. Works for both ad-hoc events and series-materialized occurrences — never mutates a
/// parent series. Rejected once the event has been cancelled.
/// </summary>
public class UpdateRaidEventCommandHandler(
    IGuildAccessService guildAccessService,
    IRaidEventRepository raidEventRepository,
    IRaidZoneRepository raidZoneRepository,
    IAuditLogService auditLogService) : ICommandHandlerAsync<UpdateRaidEventCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(UpdateRaidEventCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild.");

        if (command.GroupCount <= 0 || command.SlotsPerGroup <= 0)
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, "GroupCount and SlotsPerGroup must be positive.");

        var distinctZoneIds = command.RaidZoneIds.Distinct().ToList();
        if (distinctZoneIds.Count == 0)
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, "At least one raid zone must be targeted.");

        var existing = await raidEventRepository.GetByIdAsync(command.EventId, command.GuildId, cancellationToken);
        if (existing == null)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidEventNotFound, $"Raid event '{command.EventId}' does not exist.");

        if (existing.Status == RaidEventStatus.Cancelled)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidEventCancelled, "Cannot update a cancelled raid event.");

        var zones = await raidZoneRepository.GetByIdsAsync(distinctZoneIds, cancellationToken);
        if (zones.Count != distinctZoneIds.Count)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidZoneNotFound, "One or more raid zones do not exist.");

        var raidEvent = new RaidEvent
        {
            Id = command.EventId,
            Name = command.Name,
            BranchId = command.BranchId,
            StartsAtUtc = command.StartsAtUtc,
            GroupCount = command.GroupCount,
            SlotsPerGroup = command.SlotsPerGroup,
            UpdatedAt = DateTime.UtcNow,
        };

        var updated = await raidEventRepository.UpdateAsync(raidEvent, command.GuildId, distinctZoneIds, cancellationToken);
        if (!updated)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidEventNotFound, $"Raid event '{command.EventId}' does not exist.");

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.RaidEventUpdated,
            new Dictionary<string, string> { ["eventName"] = command.Name },
            cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Raid event updated successfully."));
    }
}
