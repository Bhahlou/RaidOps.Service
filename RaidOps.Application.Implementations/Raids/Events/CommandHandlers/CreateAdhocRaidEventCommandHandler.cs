using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Events.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Events.CommandHandlers;

/// <summary>
/// Handles <see cref="CreateAdhocRaidEventCommand"/> by verifying officer access, validating the
/// requested grid shape and target zones, then persisting a standalone raid event.
/// </summary>
public class CreateAdhocRaidEventCommandHandler(
    IGuildAccessService guildAccessService,
    IRaidEventRepository raidEventRepository,
    IRaidZoneRepository raidZoneRepository,
    IAuditLogService auditLogService) : ICommandHandlerAsync<CreateAdhocRaidEventCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(CreateAdhocRaidEventCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild.");

        if (command.GroupCount <= 0 || command.SlotsPerGroup <= 0)
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, "GroupCount and SlotsPerGroup must be positive.");

        var distinctZoneIds = command.RaidZoneIds.Distinct().ToList();
        if (distinctZoneIds.Count == 0)
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, "At least one raid zone must be targeted.");

        var zones = await raidZoneRepository.GetByIdsAsync(distinctZoneIds, cancellationToken);
        if (zones.Count != distinctZoneIds.Count)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidZoneNotFound, "One or more raid zones do not exist.");

        // PublicationStatus is left unset here, relying on RaidEvent's own Draft default —
        // ad-hoc events are never created pre-published, only PublishRaidEventCommand can do that.
        var raidEvent = new RaidEvent
        {
            GuildId = command.GuildId,
            RaidSeriesId = null,
            Name = command.Name,
            BranchId = command.BranchId,
            StartsAtUtc = command.StartsAtUtc,
            GroupCount = command.GroupCount,
            SlotsPerGroup = command.SlotsPerGroup,
            SignupMode = SignupMode.DefaultPresent,
            Status = RaidEventStatus.Scheduled,
            CreatedByDiscordId = command.RequesterDiscordId,
            CreatedAt = DateTime.UtcNow,
            TargetZones = [.. distinctZoneIds.Select(id => new RaidEventZone { RaidZoneId = id })],
        };

        var created = await raidEventRepository.AddAsync(raidEvent, cancellationToken);

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.RaidEventCreated,
            new Dictionary<string, string> { ["eventName"] = command.Name },
            cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Raid event created successfully.", new { created.Id }));
    }
}
