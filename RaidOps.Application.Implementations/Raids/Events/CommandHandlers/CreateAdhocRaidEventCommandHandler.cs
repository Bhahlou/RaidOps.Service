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
/// requested grid shape and target zones, then persisting a standalone raid event. For a
/// Signup-mode event, also posts the standing signup-call embed immediately — unlike composition
/// (only meaningful once assigned) and the "Raid published" ping (only meant for the roster once
/// official), the whole point of the signup call is to gather responses *before* the raid is built,
/// so it can't wait for publish. It stays a Draft on the site either way.
/// </summary>
public class CreateAdhocRaidEventCommandHandler(
    IRaidGridAndZoneValidator gridAndZoneValidator,
    IRaidEventRepository raidEventRepository,
    IRaidZoneRepository raidZoneRepository,
    IGuildsRepository guildsRepository,
    IGuildBranchesRepository guildBranchesRepository,
    IAuditLogService auditLogService,
    IRaidSignupAnnouncementService raidSignupAnnouncementService) : ICommandHandlerAsync<CreateAdhocRaidEventCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(CreateAdhocRaidEventCommand command, CancellationToken cancellationToken = default)
    {
        var validation = await gridAndZoneValidator.ValidateAsync(
            command.RequesterDiscordId, command.GuildId, command.GuildBranchId, command.GroupCount, command.SlotsPerGroup, command.RaidZoneIds, cancellationToken);
        if (validation.IsFailed)
            return Result<CommandResponse>.Fail(validation.Error!, validation.Detail);

        var distinctZoneIds = validation.Value!;

        int? extendsRaidEventId = null;
        if (command.ExtendsRaidEventId is { } requestedExtendsId)
        {
            var extendsTarget = await raidEventRepository.GetByIdAsync(requestedExtendsId, command.GuildBranchId, cancellationToken);
            if (extendsTarget == null)
                return Result<CommandResponse>.Fail(ResponseDetail.RaidEventNotFound, $"Raid event '{requestedExtendsId}' does not exist on this guild branch.");

            // Normalized to the chain's root (never an intermediate link) so any two events in the
            // same extension group compare equal with a single field, no graph walk needed.
            extendsRaidEventId = extendsTarget.ExtendsRaidEventId ?? extendsTarget.Id;
        }

        var branch = await guildBranchesRepository.GetByIdAsync(command.GuildBranchId, cancellationToken);

        // PublicationStatus is left unset here, relying on RaidEvent's own Draft default —
        // ad-hoc events are never created pre-published, only PublishRaidEventCommand can do that.
        var raidEvent = new RaidEvent
        {
            GuildId = command.GuildId,
            GuildBranchId = command.GuildBranchId,
            RaidSeriesId = null,
            ExtendsRaidEventId = extendsRaidEventId,
            Name = command.Name,
            StartsAtUtc = command.StartsAtUtc,
            GroupCount = command.GroupCount,
            SlotsPerGroup = command.SlotsPerGroup,
            SignupMode = command.SignupModeOverride ?? branch?.SignupMode ?? SignupMode.DefaultPresent,
            DedicatedAnnouncementChannelId = command.DedicatedAnnouncementChannelId,
            DedicatedAnnouncementChannelIsBotOwned = command.DedicatedAnnouncementChannelId is not null && command.DedicatedAnnouncementChannelIsBotOwned,
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

        if (created.SignupMode == SignupMode.Signup)
            await raidSignupAnnouncementService.PublishOrUpdateSignupCallAsync(created, cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Raid event created successfully.", new { created.Id }));
    }
}
