using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Memberships.Queries;
using RaidOps.Application.Contracts.Guilds.Memberships.Responses;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.Memberships.QueryHandlers;

/// <summary>
/// Handles <see cref="GetCharacterMembershipsQuery"/> by verifying character ownership
/// then returning all guild roster memberships for that character.
/// </summary>
public class GetCharacterMembershipsQueryHandler(
    ICharacterRepository characterRepository,
    IGuildMembershipRepository membershipRepository) : IQueryHandlerAsync<GetCharacterMembershipsQuery, List<GuildMembershipResponse>>
{
    /// <inheritdoc/>
    public async Task<Result<List<GuildMembershipResponse>>> HandleAsync(GetCharacterMembershipsQuery query, CancellationToken cancellationToken)
    {
        var character = await characterRepository.GetByIdAsync(query.CharacterId, cancellationToken);
        if (character == null)
            return Result<List<GuildMembershipResponse>>.Fail(ResponseDetail.CharacterNotFound, $"Character '{query.CharacterId}' does not exist.");

        if (character.UserDiscordId != query.RequesterDiscordId)
            return Result<List<GuildMembershipResponse>>.Fail(ResponseDetail.CharacterNotOwned, "You do not own this character.");

        var memberships = await membershipRepository.GetByCharacterIdAsync(query.CharacterId, cancellationToken);

        var response = memberships.Select(m => new GuildMembershipResponse
        {
            GuildId = m.GuildId,
            GuildName = m.Guild.Name,
            GuildIconHash = m.Guild.IconHash,
            CharacterRank = m.CharacterRank,
            JoinedAt = m.JoinedAt,
        }).ToList();

        return Result<List<GuildMembershipResponse>>.Ok(response);
    }
}
