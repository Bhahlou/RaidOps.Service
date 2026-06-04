using RaidOps.Application.Contracts.Characters.Queries;
using RaidOps.Application.Contracts.Characters.Responses;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.ExternalApplication.Contracts.Services.BNet;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Characters.QueryHandlers;

/// <summary>
/// Handles <see cref="GetAvailableCharactersQuery"/> by:
/// <list type="number">
///   <item>Loading the user's BNet account to obtain the access token and region.</item>
///   <item>Deriving the BNet profile namespace from the branch's namespace prefix.</item>
///   <item>Calling the BNet profile API to list all characters on that branch.</item>
///   <item>Flattening WoW accounts into a single list with an <c>AlreadyImported</c> flag.</item>
/// </list>
/// </summary>
public class GetAvailableCharactersQueryHandler(
    IBnetAccountRepository bnetAccountRepository,
    IBranchRepository branchRepository,
    ICharacterRepository characterRepository,
    IBnetApiService bnetApiService)
    : IQueryHandlerAsync<GetAvailableCharactersQuery, IEnumerable<AvailableCharacterDto>>
{
    /// <summary>
    /// Returns the flat list of characters available for import on the requested branch,
    /// annotated with whether each character has already been imported by the user.
    /// </summary>
    public async Task<Result<IEnumerable<AvailableCharacterDto>>> HandleAsync(
        GetAvailableCharactersQuery query,
        CancellationToken cancellationToken)
    {
        // 1. Load the user's BNet account
        var account = await bnetAccountRepository.GetByDiscordIdAsync(query.UserDiscordId, cancellationToken);
        if (account is null)
            return Result<IEnumerable<AvailableCharacterDto>>.Fail(ResponseDetail.BnetNotLinked);

        // 2. Resolve the branch and derive the profile namespace
        var branch = await branchRepository.GetByIdAsync(query.BranchId, cancellationToken);
        if (branch is null)
            return Result<IEnumerable<AvailableCharacterDto>>.Fail(ResponseDetail.BranchNotFound);

        // "dynamic-classic1x" + "-eu"  →  "profile-classic1x-eu"
        var profileNamespace = branch.BnetNamespacePrefix.Replace("dynamic", "profile") + "-" + account.Region;

        // 3. Fetch characters from the BNet API
        var bnetResponse = await bnetApiService.GetWowCharactersAsync(
            account.AccessToken,
            account.Region,
            profileNamespace,
            cancellationToken);

        // 4. Load already-imported BNet IDs for this user
        var importedIds = await characterRepository.GetBnetIdsByUserAsync(query.UserDiscordId, cancellationToken);

        // 5. Flatten WoW accounts → character list
        var characters = bnetResponse.WowAccounts
            .SelectMany(a => a.Characters)
            .Select(c => new AvailableCharacterDto
            {
                BnetCharacterId = c.Id,
                Name = c.Name,
                RealmSlug = c.Realm.Slug,
                RealmName = c.Realm.Name,
                ClassId = c.PlayableClass.Id,
                ClassName = c.PlayableClass.Name,
                RaceId = c.PlayableRace.Id,
                RaceName = c.PlayableRace.Name,
                Faction = c.Faction.Type,
                Level = c.Level,
                AlreadyImported = importedIds.Contains(c.Id)
            })
            .OrderByDescending(c => c.Level)
            .ThenBy(c => c.Name)
            .ToList();

        return Result<IEnumerable<AvailableCharacterDto>>.Ok(characters);
    }
}
