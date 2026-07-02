using RaidOps.Application.Contracts.Characters.Queries;
using RaidOps.Application.Contracts.Characters.Responses;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Characters;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Characters.QueryHandlers;

/// <summary>
/// Handles <see cref="GetCharacterQuery"/> by resolving a character from its branch/realm/name
/// route and computing the requester's viewing/editing permissions on it.
/// </summary>
public class GetCharacterQueryHandler(
    IBranchRepository branchRepository,
    ICharacterRepository characterRepository,
    IGuildMembershipRepository membershipRepository,
    IGuildAccessService guildAccessService)
    : IQueryHandlerAsync<GetCharacterQuery, CharacterDetailResponse>
{
    /// <inheritdoc/>
    public async Task<Result<CharacterDetailResponse>> HandleAsync(GetCharacterQuery query, CancellationToken cancellationToken)
    {
        var branches = await branchRepository.GetAllAsync(cancellationToken);
        var branch = branches.FirstOrDefault(b => ToSlug(b.Name) == query.BranchSlug);
        if (branch is null)
            return Result<CharacterDetailResponse>.Fail(ResponseDetail.NotFound, "Character not found.");

        var character = await characterRepository.GetByBranchRealmAndNameAsync(
            branch.Id, query.RealmSlug, query.CharacterName, cancellationToken);
        if (character is null)
            return Result<CharacterDetailResponse>.Fail(ResponseDetail.NotFound, "Character not found.");

        var isOwner = character.UserDiscordId == query.RequesterDiscordId;
        var canEditRaidSpecs = isOwner;

        if (!isOwner)
        {
            var accessLevel = await CharacterGuildAccessHelper.GetHighestAccessAsync(
                character, query.RequesterDiscordId, membershipRepository, guildAccessService, cancellationToken);

            if (accessLevel < GuildAccessLevel.Roster)
                return Result<CharacterDetailResponse>.Fail(ResponseDetail.NotFound, "Character not found.");

            canEditRaidSpecs = accessLevel >= GuildAccessLevel.Officer;
        }

        return Result<CharacterDetailResponse>.Ok(new CharacterDetailResponse
        {
            Character = CharacterMapper.ToDto(character),
            IsOwner = isOwner,
            CanEditRaidSpecs = canEditRaidSpecs,
        });
    }

    private static string ToSlug(string name) => name.ToLowerInvariant().Replace(" ", "-").Replace("_", "-");
}
