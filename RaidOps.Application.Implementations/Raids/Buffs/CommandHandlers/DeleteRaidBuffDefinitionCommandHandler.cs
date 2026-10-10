using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Buffs.Commands;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Buffs.CommandHandlers;

/// <summary>Handles <see cref="DeleteRaidBuffDefinitionCommand"/>. Admin-only: the owner gate lives on the controller.</summary>
public class DeleteRaidBuffDefinitionCommandHandler(IRaidBuffDefinitionsRepository definitionsRepository)
    : ICommandHandlerAsync<DeleteRaidBuffDefinitionCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(DeleteRaidBuffDefinitionCommand command, CancellationToken cancellationToken = default)
    {
        var deleted = await definitionsRepository.DeleteAsync(command.Id, cancellationToken);

        return deleted
            ? Result<CommandResponse>.Ok(new CommandResponse("Raid buff definition deleted successfully."))
            : Result<CommandResponse>.Fail(ResponseDetail.NotFound, $"Raid buff definition '{command.Id}' does not exist.");
    }
}
