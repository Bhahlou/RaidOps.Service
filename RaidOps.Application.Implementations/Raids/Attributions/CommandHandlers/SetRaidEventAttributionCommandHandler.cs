using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Attributions.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids;
using RaidOps.Domain.Models.Raids.Attributions;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Attributions.CommandHandlers;

/// <summary>
/// Handles <see cref="SetRaidEventAttributionCommand"/> by validating the officer's access, the
/// definition/cell coordinates, that the target cell is a name slot the character actually meets
/// the class/role/spec requirements of, and — critically — that the target character is already
/// seated in this event (has a <c>RaidSlotAssignment</c> for it), before filling the slot.
/// </summary>
public class SetRaidEventAttributionCommandHandler(
    IGuildAccessService guildAccessService,
    IRaidEventRepository raidEventRepository,
    IGuildAttributionDefinitionsRepository definitionsRepository,
    IRaidEventAttributionsRepository attributionsRepository,
    IAuditLogService auditLogService) : ICommandHandlerAsync<SetRaidEventAttributionCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(SetRaidEventAttributionCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, command.GuildBranchId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild branch.");

        var raidEvent = await raidEventRepository.GetByIdAsync(command.EventId, command.GuildBranchId, cancellationToken);
        if (raidEvent == null)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidEventNotFound, $"Raid event '{command.EventId}' does not exist.");

        var definition = await definitionsRepository.GetByIdAsync(command.DefinitionId, cancellationToken);
        if (definition == null || definition.GuildId != command.GuildId)
            return Result<CommandResponse>.Fail(ResponseDetail.AttributionDefinitionNotFound, $"Definition '{command.DefinitionId}' does not exist on this guild.");

        var cell = definition.Cells.FirstOrDefault(c => c.Id == command.CellId);
        if (cell == null)
            return Result<CommandResponse>.Fail(ResponseDetail.AttributionCellNotFound, $"Cell '{command.CellId}' does not exist on this definition.");
        if (cell.Kind != AttributionCellKind.NameSlot)
            return Result<CommandResponse>.Fail(ResponseDetail.AttributionCellNotNameSlot, $"Cell '{command.CellId}' is not a fillable name slot.");

        if (command.InstanceIndex < 0 || command.InstanceIndex >= 50 || (!definition.IsRepeatable && command.InstanceIndex != 0))
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidInstanceIndex, $"Instance index '{command.InstanceIndex}' is invalid for this definition.");

        var assignment = raidEvent.Assignments.FirstOrDefault(a => a.CharacterId == command.CharacterId);
        if (assignment == null)
            return Result<CommandResponse>.Fail(ResponseDetail.CharacterNotSeatedInEvent, "Character is not seated in this raid event.");

        if (!MeetsSlotRequirement(cell, assignment))
            return Result<CommandResponse>.Fail(ResponseDetail.CharacterDoesNotMeetSlotRequirement, "Character does not match this slot's class/role/spec requirement.");

        await attributionsRepository.SetAsync(command.EventId, cell.Id, definition.Id, command.InstanceIndex, command.CharacterId, command.RequesterDiscordId, cancellationToken);

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.RaidEventAttributionUpdated,
            new Dictionary<string, string> { ["definitionId"] = command.DefinitionId.ToString(), ["characterId"] = command.CharacterId.ToString() },
            cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Attribution slot filled successfully."));
    }

    private static bool MeetsSlotRequirement(AttributionDefinitionCell cell, RaidSlotAssignment assignment) =>
        (cell.RequiredClassIds.Count == 0 || cell.RequiredClassIds.Contains(assignment.Character.ClassId))
        && (cell.RequiredRoles.Count == 0 || cell.RequiredRoles.Contains(assignment.Spec.Role))
        && (cell.RequiredSpecIds.Count == 0 || cell.RequiredSpecIds.Contains(assignment.SpecId));
}
