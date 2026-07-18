using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Roster.Queries;
using RaidOps.Application.Contracts.Guilds.Roster.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.Roster.QueryHandlers;

/// <summary>
/// Handles <see cref="GetGuildRosterQuery"/> by returning every active character on a
/// registered guild's roster, ordered by raid-composition rank then character name.
/// </summary>
public class GetGuildRosterQueryHandler(
    IGuildsRepository guildsRepository,
    IGuildAccessService guildAccessService,
    IGuildMembershipRepository membershipRepository,
    IUsersRepository usersRepository) : IQueryHandlerAsync<GetGuildRosterQuery, List<GuildRosterMemberResponse>>
{
    /// <inheritdoc/>
    public async Task<Result<List<GuildRosterMemberResponse>>> HandleAsync(GetGuildRosterQuery query, CancellationToken cancellationToken)
    {
        var guild = await guildsRepository.GetByIdAsync(query.GuildId, cancellationToken);
        if (guild == null || !guild.IsRegistered)
            return Result<List<GuildRosterMemberResponse>>.Fail(ResponseDetail.GuildNotFound, $"Guild '{query.GuildId}' does not exist or is not registered.");

        var accessLevel = await guildAccessService.GetAccessLevelAsync(query.RequesterDiscordId, query.GuildId, cancellationToken);
        if (accessLevel < GuildAccessLevel.Roster)
            return Result<List<GuildRosterMemberResponse>>.Fail(ResponseDetail.RosterAccessDenied, "You do not have access to this guild's roster.");

        var memberships = await membershipRepository.GetByGuildIdAsync(query.GuildId, cancellationToken);

        var playerIds = memberships.Select(m => m.Character.UserDiscordId).Distinct().ToList();
        var players = await usersRepository.FindAsync(u => playerIds.Contains(u.DiscordId), cancellationToken);
        var playersById = players.ToDictionary(u => u.DiscordId);

        var isOfficerOrAbove = accessLevel >= GuildAccessLevel.Officer;

        var roster = new List<GuildRosterMemberResponse>();
        foreach (var membership in memberships)
        {
            var isOwnRow = membership.Character.UserDiscordId == query.RequesterDiscordId;
            var canExclude = isOfficerOrAbove
                && (isOwnRow || await guildAccessService.OutranksAsync(query.GuildId, query.RequesterDiscordId, membership.Character.UserDiscordId, cancellationToken));

            roster.Add(GuildRosterMapper.ToDto(membership, playersById, canExclude));
        }

        var sortedRoster = roster
            .OrderBy(m => m.CharacterRank)
            .ThenBy(m => m.CharacterName)
            .ToList();

        return Result<List<GuildRosterMemberResponse>>.Ok(sortedRoster);
    }
}
