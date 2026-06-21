using RaidOps.Application.Contracts.Characters.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Domain.Models.Character;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Characters.CommandHandlers;

/// <summary>
/// Handles <see cref="SetCharacterRaidSpecsCommand"/> by validating the requested specs against
/// the character's class and replacing its raid-viable spec set. Idempotent — also used to edit
/// a previously set raid spec selection.
/// </summary>
public class SetCharacterRaidSpecsCommandHandler(
    ICharacterRepository characterRepository,
    ISpecRepository specRepository)
    : ICommandHandlerAsync<SetCharacterRaidSpecsCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(
        SetCharacterRaidSpecsCommand command,
        CancellationToken cancellationToken = default)
    {
        var viableIds = command.ViableSpecIds.Distinct().ToList();

        if (viableIds.Count == 0)
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, "At least one viable spec is required.");

        if (!viableIds.Contains(command.MainSpecId))
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, "MainSpecId must be included in ViableSpecIds.");

        var character = await characterRepository.GetByIdAsync(command.CharacterId, cancellationToken);
        if (character is null)
            return Result<CommandResponse>.Fail(ResponseDetail.CharacterNotFound, $"Character '{command.CharacterId}' does not exist.");

        if (character.UserDiscordId != command.UserDiscordId)
            return Result<CommandResponse>.Fail(ResponseDetail.CharacterNotOwned, "You do not own this character.");

        var allSpecs = (await specRepository.GetAllAsync(cancellationToken)).ToDictionary(s => s.Id);

        foreach (var specId in viableIds)
        {
            if (!allSpecs.TryGetValue(specId, out var spec) || spec.ClassId != character.ClassId)
                return Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, $"Spec '{specId}' is not valid for this character's class.");
        }

        var raidSpecs = viableIds.Select(id => new CharacterRaidSpec
        {
            CharacterId = command.CharacterId,
            SpecId = id,
            IsMain = id == command.MainSpecId,
        });

        await characterRepository.UpsertRaidSpecsAsync(command.CharacterId, raidSpecs, cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Raid specs updated successfully."));
    }
}
