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
/// Handles <see cref="CreateAdhocRaidEventCommand"/> by verifying officer access, validating the
/// requested grid shape and target zones, then persisting a standalone raid event.
/// </summary>
public class CreateAdhocRaidEventCommandHandler(
    IRaidGridAndZoneValidator gridAndZoneValidator,
    IRaidEventRepository raidEventRepository,
    IRaidZoneRepository raidZoneRepository,
    IGuildsRepository guildsRepository,
    IAuditLogService auditLogService) : ICommandHandlerAsync<CreateAdhocRaidEventCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(CreateAdhocRaidEventCommand command, CancellationToken cancellationToken = default)
    {
        var validation = await gridAndZoneValidator.ValidateAsync(
            command.RequesterDiscordId, command.GuildId, command.GuildBranchId, command.GroupCount, command.SlotsPerGroup, command.RaidZoneIds, cancellationToken);
        if (validation.IsFailed)
            return Result<CommandResponse>.Fail(validation.Error!, validation.Detail);

        var distinctZoneIds = validation.Value!;

        // PublicationStatus is left unset here, relying on RaidEvent's own Draft default —
        // ad-hoc events are never created pre-published, only PublishRaidEventCommand can do that.
        var raidEvent = new RaidEvent
        {
            GuildId = command.GuildId,
            GuildBranchId = command.GuildBranchId,
            RaidSeriesId = null,
            Name = command.Name,
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

        var zones = await raidZoneRepository.GetByIdsAsync(distinctZoneIds, cancellationToken);
        var guild = await guildsRepository.GetByIdAsync(command.GuildId, cancellationToken);
        var startsAtLocal = GuildTimeHelper.ToGuildLocalDateTime(command.StartsAtUtc, guild?.Timezone);

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.RaidEventCreated,
            new Dictionary<string, string>
            {
                ["eventName"] = command.Name,
                ["startsAtLocal"] = startsAtLocal.ToString("yyyy-MM-dd HH:mm"),
                ["raidZoneNames"] = string.Join(", ", zones.Select(z => z.Name)),
            },
            cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Raid event created successfully.", new { created.Id }));
    }
}
