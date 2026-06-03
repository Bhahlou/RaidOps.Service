using RaidOps.Application.Contracts.Characters.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Characters;

/// <summary>
/// Handles <see cref="DeactivateCharacterCommand"/> by setting
/// <c>IsActiveInRaidOps = false</c> for the given character.
/// Returns a failure if the character does not exist or does not belong to the requesting user.
/// </summary>
public class DeactivateCharacterCommandHandler(ICharacterRepository characterRepository)
    : ICommandHandlerAsync<DeactivateCharacterCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(
        DeactivateCharacterCommand command,
        CancellationToken cancellationToken = default)
    {
        var deactivated = await characterRepository.DeactivateAsync(
            command.CharacterId, command.UserDiscordId, cancellationToken);

        return deactivated
            ? Result<CommandResponse>.Ok(new CommandResponse("Character deactivated successfully."))
            : Result<CommandResponse>.Fail(ResponseDetail.NotFound);
    }
}
