using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Memberships.Queries;
using RaidOps.Application.Contracts.Guilds.Memberships.Responses;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.Memberships.QueryHandlers;

/// <summary>
/// Handles <see cref="GetMyMembershipsInGuildQuery"/> by returning all characters owned by
/// the requesting user that are on the specified guild's roster.
/// </summary>
public class GetMyMembershipsInGuildQueryHandler(
    IGuildMembershipRepository membershipRepository) : IQueryHandlerAsync<GetMyMembershipsInGuildQuery, List<CharacterInGuildResponse>>
{
    /// <inheritdoc/>
    public async Task<Result<List<CharacterInGuildResponse>>> HandleAsync(GetMyMembershipsInGuildQuery query, CancellationToken cancellationToken)
    {
        var memberships = await membershipRepository.GetByGuildIdAndUserAsync(
            query.GuildId,
            query.RequesterDiscordId,
            cancellationToken);

        var response = memberships.Select(m =>
        {
            var activeState = m.Character.ExpansionStates.FirstOrDefault(s => s.IsActive)
                           ?? m.Character.ExpansionStates.OrderByDescending(s => s.Level).FirstOrDefault();

            return new CharacterInGuildResponse
            {
                CharacterId = m.CharacterId,
                Name = m.Character.Name,
                RealmName = m.Character.Realm.Name,
                ClassName = m.Character.Class.Name,
                ClassColor = "#" + m.Character.Class.Color,
                AvatarUrl = m.Character.AvatarUrl,
                GuildName = activeState?.GuildName,
                CharacterRank = m.CharacterRank,
                JoinedAt = m.JoinedAt,
            };
        }).ToList();

        return Result<List<CharacterInGuildResponse>>.Ok(response);
    }
}
