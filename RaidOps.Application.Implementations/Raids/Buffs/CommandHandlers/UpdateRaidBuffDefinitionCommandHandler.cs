using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Buffs.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Buffs.Services;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Buffs.CommandHandlers;

/// <summary>
/// Handles <see cref="UpdateRaidBuffDefinitionCommand"/>: validates the new values like an upsert would, refuses
/// a spell another definition of the same expansion already uses, then overwrites the definition in place.
/// Admin-only: the owner gate lives on the controller.
/// </summary>
public class UpdateRaidBuffDefinitionCommandHandler(
    IRaidBuffDefinitionValidationService validationService,
    IRaidBuffDefinitionsRepository definitionsRepository) : ICommandHandlerAsync<UpdateRaidBuffDefinitionCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(UpdateRaidBuffDefinitionCommand command, CancellationToken cancellationToken = default)
    {
        var existing = await definitionsRepository.GetByIdAsync(command.Id, cancellationToken);
        if (existing is null)
            return Result<CommandResponse>.Fail(ResponseDetail.NotFound, $"Raid buff definition '{command.Id}' does not exist.");

        var errors = await validationService.ValidateAsync(existing.ExpansionId, [command.Definition], cancellationToken);
        if (errors.Count > 0)
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, string.Join(" ", errors));

        var clash = await definitionsRepository.GetBySpellAsync(existing.ExpansionId, command.Definition.SpellId, cancellationToken);
        if (clash is not null && clash.Id != command.Id)
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, $"Spell {command.Definition.SpellId} already has a definition on this expansion.");

        await definitionsRepository.UpdateAsync(command.Id, RaidBuffDefinitionMapper.ToEntity(command.Definition), cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Raid buff definition updated successfully."));
    }
}
