using Microsoft.Extensions.Logging;
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
    IBnetApiService bnetApiService,
    ILogger<SyncBnetCharactersCommandHandler> logger)
    : ICommandHandlerAsync<SyncBnetCharactersCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(
        SyncBnetCharactersCommand command,
        CancellationToken cancellationToken = default)
    {
        var accounts = await bnetAccountRepository.GetAllByDiscordIdAsync(command.UserDiscordId, cancellationToken);
        if (accounts.Count == 0)
        {
            logger.LogWarning(
                "BNet sync failed for discord user {DiscordId}: no linked BNet account",
                command.UserDiscordId);
            return Result<CommandResponse>.Fail(ResponseDetail.BnetNotLinked);
        }

        var branch = await branchRepository.GetByIdAsync(command.BranchId, cancellationToken);
        if (branch is null)
        {
            logger.LogWarning(
                "BNet sync failed for discord user {DiscordId}: branch {BranchId} not found",
                command.UserDiscordId, command.BranchId);
            return Result<CommandResponse>.Fail(ResponseDetail.BranchNotFound);
        }

        // The character-list endpoint below only returns basic info (name, level, race, class,
        // faction, gender) — no avatar, current guild or item level. Look up what's already in
        // the DB so those richer fields (populated by activation/resync) aren't wiped below.
        var existingCharacters = await characterRepository.GetByUserWithDetailsAsync(
            command.UserDiscordId, activeOnly: false, cancellationToken);
        var existingByBnetId = existingCharacters.ToDictionary(c => (c.BnetCharacterId, c.BranchId));

        var synced = 0;

        foreach (var account in accounts)
        {
            var profileNamespace = branch.BnetNamespacePrefix.Replace("dynamic", "profile") + "-" + account.Region;

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "Syncing BNet characters for discord user {DiscordId}, bnetId {BnetId}, branch {BranchId}, namespace {Namespace}, region {Region}",
                    command.UserDiscordId, account.BnetId, command.BranchId, profileNamespace, account.Region);
            }

            var bnetResponse = await bnetApiService.GetWowCharactersAsync(
                account.AccessToken,
                account.Region,
                profileNamespace,
                cancellationToken);

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
                        SourceBnetId = account.BnetId,
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
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "BNet sync completed for discord user {DiscordId}, branch {BranchId}: {SyncedCount} character(s) synced across {AccountCount} account(s)",
                command.UserDiscordId, command.BranchId, synced, accounts.Count);
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
