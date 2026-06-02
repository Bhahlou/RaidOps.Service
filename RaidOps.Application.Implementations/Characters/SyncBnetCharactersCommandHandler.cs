using RaidOps.Application.Contracts.Characters.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.ExternalApplication.Contracts.Services.BNet;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Characters;

/// <summary>
/// Handles <see cref="SyncBnetCharactersCommand"/> by fetching all WoW characters
/// from the user's BNet account for the given branch and upserting them in the database.
/// </summary>
public class SyncBnetCharactersCommandHandler(
    IBnetAccountRepository bnetAccountRepository,
    IBranchRepository branchRepository,
    IRealmRepository realmRepository,
    ICharacterRepository characterRepository,
    IBnetApiService bnetApiService)
    : ICommandHandlerAsync<SyncBnetCharactersCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(
        SyncBnetCharactersCommand command,
        CancellationToken cancellationToken = default)
    {
        var account = await bnetAccountRepository.GetByDiscordIdAsync(command.UserDiscordId, cancellationToken);
        if (account is null)
            return Result<CommandResponse>.Fail(ResponseDetail.BnetNotLinked);

        var branch = await branchRepository.GetByIdAsync(command.BranchId, cancellationToken);
        if (branch is null)
            return Result<CommandResponse>.Fail(ResponseDetail.BranchNotFound);

        var profileNamespace = branch.BnetNamespacePrefix.Replace("dynamic", "profile") + "-" + account.Region;

        var bnetResponse = await bnetApiService.GetWowCharactersAsync(
            account.AccessToken,
            account.Region,
            profileNamespace,
            cancellationToken);

        var synced = 0;

        foreach (var wowAccount in bnetResponse.WowAccounts)
        {
            foreach (var c in wowAccount.Characters)
            {
                var realm = await realmRepository.GetBySlugAndBranchAsync(c.Realm.Slug, command.BranchId, cancellationToken);
                if (realm is null)
                {
                    realm = await realmRepository.AddAsync(new Realm
                    {
                        Slug = c.Realm.Slug,
                        Name = c.Realm.Name,
                        Region = account.Region,
                        BranchId = command.BranchId
                    }, cancellationToken);
                }

                var character = await characterRepository.UpsertAsync(new Character
                {
                    BnetCharacterId = c.Id,
                    Name = c.Name,
                    Faction = ParseFaction(c.Faction.Type),
                    Gender = ParseGender(c.Gender.Type),
                    UserDiscordId = command.UserDiscordId,
                    BranchId = command.BranchId,
                    RealmId = realm.Id,
                    RaceId = c.PlayableRace.Id,
                    ClassId = c.PlayableClass.Id
                }, cancellationToken);

                await characterRepository.UpsertExpansionStateAsync(new CharacterExpansionState
                {
                    CharacterId = character.Id,
                    ExpansionId = branch.CurrentExpansionId,
                    Level = c.Level,
                    IsActive = true
                }, cancellationToken);

                synced++;
            }
        }

        return Result<CommandResponse>.Ok(new CommandResponse($"{synced} character(s) synced successfully."));
    }

    private static Faction ParseFaction(string type) => type.ToUpperInvariant() switch
    {
        "ALLIANCE" => Faction.Alliance,
        "HORDE" => Faction.Horde,
        _ => Faction.Neutral
    };

    private static Gender ParseGender(string type) => type.ToUpperInvariant() switch
    {
        "FEMALE" => Gender.Female,
        _ => Gender.Male
    };
}
