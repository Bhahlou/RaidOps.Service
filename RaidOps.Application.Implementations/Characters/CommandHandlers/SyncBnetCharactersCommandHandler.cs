using RaidOps.Application.Contracts.Characters.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.ExternalApplication.Contracts.Services.BNet;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Characters.CommandHandlers;

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

        // The character-list endpoint above only returns basic info (name, level, race, class,
        // faction, gender) — no avatar, current guild or item level. Look up what's already in
        // the DB so those richer fields (populated by activation/resync) aren't wiped below.
        var existingCharacters = await characterRepository.GetByUserWithDetailsAsync(
            command.UserDiscordId, activeOnly: false, cancellationToken);
        var existingByBnetId = existingCharacters.ToDictionary(c => (c.BnetCharacterId, c.BranchId));

        var synced = 0;

        foreach (var wowAccount in bnetResponse.WowAccounts)
        {
            foreach (var c in wowAccount.Characters)
            {
                var realm = await realmRepository.GetBySlugAndBranchAsync(c.Realm.Slug, command.BranchId, cancellationToken);
                realm ??= await realmRepository.AddAsync(new Realm
                    {
                        Slug = c.Realm.Slug,
                        Name = c.Realm.Name,
                        Region = account.Region,
                        BranchId = command.BranchId
                    }, cancellationToken);

                existingByBnetId.TryGetValue((c.Id, command.BranchId), out var existingCharacter);

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
                    ClassId = c.PlayableClass.Id,
                    AvatarUrl = existingCharacter?.AvatarUrl,
                }, cancellationToken);

                var existingState = existingCharacter?.ExpansionStates.FirstOrDefault(s => s.ExpansionId == branch.CurrentExpansionId);

                await characterRepository.UpsertExpansionStateAsync(new CharacterExpansionState
                {
                    CharacterId = character.Id,
                    ExpansionId = branch.CurrentExpansionId,
                    Level = c.Level,
                    ItemLevel = existingState?.ItemLevel,
                    GuildName = existingState?.GuildName,
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
