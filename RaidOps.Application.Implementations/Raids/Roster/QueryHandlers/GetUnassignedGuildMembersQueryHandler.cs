using RaidOps.Application.Contracts.Characters.Responses;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Roster.Queries;
using RaidOps.Application.Contracts.Raids.Roster.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Helpers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Roster.QueryHandlers;

/// <summary>
/// Handles <see cref="GetUnassignedGuildMembersQuery"/> by returning every active roster
/// character that isn't assigned to any non-cancelled, <b>published</b> raid event starting within
/// the requested range — powers the raid builder's "unassigned members" panel. Draft-only
/// assignments don't count, regardless of the requester's own access level: even an officer's view
/// reflects the official, published schedule rather than their in-progress drafts.
/// </summary>
public class GetUnassignedGuildMembersQueryHandler(
    IGuildAccessService guildAccessService,
    IGuildsRepository guildsRepository,
    IGuildMembershipRepository guildMembershipRepository,
    IRaidCompositionRepository raidCompositionRepository,
    IUsersRepository usersRepository) : IQueryHandlerAsync<GetUnassignedGuildMembersQuery, List<UnassignedMemberResponse>>
{
    /// <inheritdoc/>
    public async Task<Result<List<UnassignedMemberResponse>>> HandleAsync(GetUnassignedGuildMembersQuery query, CancellationToken cancellationToken)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(query.RequesterDiscordId, query.GuildId, query.GuildBranchId, cancellationToken);
        if (accessLevel < GuildAccessLevel.Roster)
            return Result<List<UnassignedMemberResponse>>.Fail(ResponseDetail.Forbidden, "User is not on this guild branch's roster.");

        if (query.RangeEnd < query.RangeStart)
            return Result<List<UnassignedMemberResponse>>.Fail(ResponseDetail.InvalidRequest, "RangeEnd must be on or after RangeStart.");

        var guild = await guildsRepository.GetByIdAsync(query.GuildId, cancellationToken);
        if (guild == null)
            return Result<List<UnassignedMemberResponse>>.Fail(ResponseDetail.GuildNotFound, $"Guild '{query.GuildId}' does not exist.");

        var rangeStartUtc = GuildTimeHelper.FromGuildLocal(query.RangeStart.ToDateTime(TimeOnly.MinValue), guild.Timezone);
        var rangeEndUtc = GuildTimeHelper.FromGuildLocal(query.RangeEnd.ToDateTime(new TimeOnly(23, 59, 59)), guild.Timezone);

        var memberships = await guildMembershipRepository.GetByGuildBranchIdAsync(query.GuildBranchId, cancellationToken);
        var assignedCharacterIds = await raidCompositionRepository.GetAssignedCharacterIdsInRangeAsync(query.GuildBranchId, rangeStartUtc, rangeEndUtc, cancellationToken);

        var unassigned = memberships.Where(m => !assignedCharacterIds.Contains(m.CharacterId)).ToList();

        var playerIds = unassigned.Select(m => m.Character.UserDiscordId).Distinct().ToList();
        var players = await usersRepository.FindAsync(u => playerIds.Contains(u.DiscordId), cancellationToken);
        var playersById = players.ToDictionary(u => u.DiscordId);

        var response = unassigned
            .Select(m => MapMember(m, playersById))
            .OrderBy(m => m.CharacterRank)
            .ThenBy(m => m.CharacterName)
            .ToList();

        return Result<List<UnassignedMemberResponse>>.Ok(response);
    }

    private static UnassignedMemberResponse MapMember(GuildMembership membership, Dictionary<string, User> playersById)
    {
        var character = membership.Character;
        playersById.TryGetValue(character.UserDiscordId, out var player);

        return new UnassignedMemberResponse
        {
            CharacterId = character.Id,
            CharacterName = character.Name,
            ClassId = character.ClassId,
            ClassName = character.Class.Name,
            ClassColor = "#" + character.Class.Color,
            BranchId = character.BranchId,
            BranchName = character.Branch.Name,
            AvatarUrl = character.AvatarUrl,
            PlayerDiscordId = character.UserDiscordId,
            PlayerName = player?.Name,
            RaidSpecs = [.. character.RaidSpecs
                .OrderByDescending(rs => rs.IsMain)
                .Select(rs => new CharacterRaidSpecDto
                {
                    SpecId = rs.SpecId,
                    Name = rs.Spec.Name,
                    IconUrl = rs.Spec.IconUrl,
                    IsMain = rs.IsMain,
                })],
            CharacterRank = membership.CharacterRank,
        };
    }
}
