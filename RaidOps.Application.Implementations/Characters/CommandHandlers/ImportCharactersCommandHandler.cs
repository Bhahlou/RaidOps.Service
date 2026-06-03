using RaidOps.Application.Contracts.Characters.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Characters.CommandHandlers;

/// <summary>
/// Handles <see cref="ImportCharactersCommand"/> by:
/// <list type="number">
///   <item>Resolving the user's BNet region from the linked account.</item>
///   <item>Resolving or creating each character's <see cref="Realm"/> record.</item>
///   <item>Upserting each <see cref="Character"/> using its BNet ID as the natural key.</item>
///   <item>Creating or refreshing the <see cref="CharacterExpansionState"/> for the branch's current expansion.</item>
/// </list>
/// </summary>
public class ImportCharactersCommandHandler(
    IBnetAccountRepository bnetAccountRepository,
    IBranchRepository branchRepository,
    IRealmRepository realmRepository,
    ICharacterRepository characterRepository)
    : ICommandHandlerAsync<ImportCharactersCommand>
{
    /// <summary>
    /// Processes the import, upserting characters and their expansion states.
    /// Returns a <see cref="CommandResponse"/> with the count of imported characters.
    /// </summary>
    public async Task<Result<CommandResponse>> HandleAsync(
        ImportCharactersCommand command,
        CancellationToken cancellationToken = default)
    {
        // Resolve the user's BNet region — stored on the linked account
        var account = await bnetAccountRepository.GetByDiscordIdAsync(command.UserDiscordId, cancellationToken);
        if (account is null)
            return Result<CommandResponse>.Fail(ResponseDetail.BnetNotLinked);

        var region = account.Region;

        var branch = await branchRepository.GetByIdAsync(command.BranchId, cancellationToken);
        if (branch is null)
            return Result<CommandResponse>.Fail(ResponseDetail.BranchNotFound);

        var imported = 0;

        foreach (var dto in command.Characters)
        {
            // Resolve or cache the realm (scoped per branch, not per region, since each
            // branch has its own realm pool; region is stored for display purposes only)
            var realm = await realmRepository.GetBySlugAndBranchAsync(dto.RealmSlug, command.BranchId, cancellationToken);
            if (realm is null)
            {
                realm = await realmRepository.AddAsync(new Realm
                {
                    Slug = dto.RealmSlug,
                    Name = dto.RealmName,
                    Region = region,
                    BranchId = command.BranchId
                }, cancellationToken);
            }

            // Upsert the character
            var character = await characterRepository.UpsertAsync(new Character
            {
                BnetCharacterId = dto.BnetCharacterId,
                Name = dto.Name,
                Faction = ParseFaction(dto.Faction),
                UserDiscordId = command.UserDiscordId,
                RealmId = realm.Id,
                RaceId = dto.RaceId,
                ClassId = dto.ClassId
            }, cancellationToken);

            // Create / refresh the expansion state for this branch's current expansion
            await characterRepository.UpsertExpansionStateAsync(new CharacterExpansionState
            {
                CharacterId = character.Id,
                ExpansionId = branch.CurrentExpansionId,
                Level = dto.Level,
                IsActive = true
            }, cancellationToken);

            imported++;
        }

        return Result<CommandResponse>.Ok(new CommandResponse($"{imported} character(s) imported successfully."));
    }

    /// <summary>
    /// Converts the BNet API faction type string (e.g. "ALLIANCE") to the domain <see cref="Faction"/> enum.
    /// Falls back to <see cref="Faction.Neutral"/> for unknown values (e.g. Pandaren before faction selection).
    /// </summary>
    private static Faction ParseFaction(string type) => type.ToUpperInvariant() switch
    {
        "ALLIANCE" => Faction.Alliance,
        "HORDE"    => Faction.Horde,
        _          => Faction.Neutral
    };
}
