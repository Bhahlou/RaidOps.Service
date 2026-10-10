using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Signups.Queries;
using RaidOps.Application.Contracts.Raids.Signups.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Signups.QueryHandlers;

/// <inheritdoc cref="GetMyRosterCharactersQuery"/>
public class GetMyRosterCharactersQueryHandler(
    IGuildAccessService guildAccessService,
    IGuildMembershipRepository guildMembershipRepository,
    ICharacterRepository characterRepository) : IQueryHandlerAsync<GetMyRosterCharactersQuery, List<RaidSignupCharacterResponse>>
{
    /// <inheritdoc/>
    public async Task<Result<List<RaidSignupCharacterResponse>>> HandleAsync(GetMyRosterCharactersQuery query, CancellationToken cancellationToken)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(query.RequesterDiscordId, query.GuildId, query.GuildBranchId, cancellationToken);
        if (accessLevel < GuildAccessLevel.Roster)
            return Result<List<RaidSignupCharacterResponse>>.Fail(ResponseDetail.Forbidden, "User is not on this guild branch's roster.");

        var memberships = await guildMembershipRepository.GetByGuildBranchIdAsync(query.GuildBranchId, cancellationToken);
        var myCharacters = memberships
            .Where(m => m.Character.UserDiscordId == query.RequesterDiscordId)
            .Select(m => m.Character)
            .ToList();

        var raidSpecs = await characterRepository.GetRaidSpecsForCharactersAsync(myCharacters.Select(c => c.Id), cancellationToken);
        var raidSpecsByCharacterId = raidSpecs.ToLookup(rs => rs.CharacterId);

        var response = myCharacters
            .Select(c => new RaidSignupCharacterResponse
            {
                CharacterId = c.Id,
                CharacterName = c.Name,
                ClassId = c.ClassId,
                BranchName = c.Branch.Name,
                RealmSlug = c.Realm.Slug,
                RaidSpecs = [.. raidSpecsByCharacterId[c.Id]
                    .OrderByDescending(rs => rs.IsMain)
                    .Select(rs => new RaidSignupSpecResponse { SpecId = rs.SpecId, SpecName = rs.Spec.Name, IsMain = rs.IsMain })],
            })
            .OrderBy(c => c.CharacterName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Result<List<RaidSignupCharacterResponse>>.Ok(response);
    }
}
