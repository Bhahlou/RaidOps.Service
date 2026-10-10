using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Buffs.Commands;
using RaidOps.Application.Contracts.Raids.Buffs.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Buffs.Services;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Buffs.CommandHandlers;

/// <summary>
/// Handles <see cref="UpsertRaidBuffDefinitionsCommand"/> by validating every entry against the
/// reference data (spells on the expansion, classes, specs) and only then writing them in one save.
/// Admin-only: the owner gate lives on the controller, like the manual spell sync.
/// </summary>
public class UpsertRaidBuffDefinitionsCommandHandler(
    IExpansionRepository expansionRepository,
    IRaidBuffDefinitionValidationService validationService,
    IRaidBuffDefinitionsRepository definitionsRepository) : ICommandHandlerAsync<UpsertRaidBuffDefinitionsCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(UpsertRaidBuffDefinitionsCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Definitions.Count == 0)
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, "At least one definition is required.");

        if (!(await expansionRepository.GetAllAsync(cancellationToken)).Any(e => e.Id == command.ExpansionId))
            return Result<CommandResponse>.Fail(ResponseDetail.NotFound, $"Expansion '{command.ExpansionId}' does not exist.");

        var errors = await validationService.ValidateAsync(command.ExpansionId, command.Definitions, cancellationToken);
        if (errors.Count > 0)
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, string.Join(" ", errors));

        var (created, updated, deleted) = await definitionsRepository.UpsertAsync(
            command.ExpansionId,
            command.Definitions.Select(RaidBuffDefinitionMapper.ToEntity).ToList(),
            command.PruneMissing,
            cancellationToken);

        var summary = new RaidBuffUpsertSummary { Created = created, Updated = updated, Deleted = deleted };
        return Result<CommandResponse>.Ok(new CommandResponse("Raid buff definitions saved successfully.", summary));
    }
}
