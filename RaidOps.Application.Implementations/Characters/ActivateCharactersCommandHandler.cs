using RaidOps.Application.Contracts.Characters.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Characters;

/// <summary>
/// Handles <see cref="ActivateCharactersCommand"/> by marking the given characters
/// as active in RaidOps for the requesting user.
/// </summary>
public class ActivateCharactersCommandHandler(ICharacterRepository characterRepository)
    : ICommandHandlerAsync<ActivateCharactersCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(
        ActivateCharactersCommand command,
        CancellationToken cancellationToken = default)
    {
        await characterRepository.ActivateAsync(command.CharacterIds, command.UserDiscordId, cancellationToken);
        return Result<CommandResponse>.Ok(new CommandResponse("Characters activated successfully."));
    }
}
