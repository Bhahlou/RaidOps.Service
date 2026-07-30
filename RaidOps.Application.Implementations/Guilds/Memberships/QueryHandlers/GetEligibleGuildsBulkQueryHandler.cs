using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Memberships.Queries;
using RaidOps.Application.Contracts.Guilds.Memberships.Responses;
using RaidOps.Application.Implementations.Guilds.Access;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Discord;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.Memberships.QueryHandlers;

/// <summary>
/// Handles <see cref="GetEligibleGuildsBulkQuery"/> by returning, for each registered guild the
/// user belongs to on Discord, the subset of their active characters whose WoW branch is active
/// and configured on that guild, not yet on that guild's roster, and passing the branch's
/// roster-mode access check.
/// All characters and memberships are fetched in bulk to avoid N+1 queries.
/// </summary>
public class GetEligibleGuildsBulkQueryHandler(
    ICharacterRepository characterRepository,
    IGuildsRepository guildsRepository,
    IGuildBranchesRepository guildBranchesRepository,
    IUserGuildsRepository userGuildsRepository,
    IGuildMembershipRepository membershipRepository,
    IDiscordBotService discordBotService) : IQueryHandlerAsync<GetEligibleGuildsBulkQuery, List<GuildEligibilityResponse>>
{
    /// <inheritdoc/>
    public async Task<Result<List<GuildEligibilityResponse>>> HandleAsync(GetEligibleGuildsBulkQuery query, CancellationToken cancellationToken)
    {
        var characters = (await characterRepository.GetByUserWithDetailsAsync(
            query.RequesterDiscordId, activeOnly: true, cancellationToken)).ToList();

        if (characters.Count == 0)
            return Result<List<GuildEligibilityResponse>>.Ok([]);

        var characterIds = characters.Select(c => c.Id).ToList();
        var allMemberships = await membershipRepository.GetByCharacterIdsAsync(characterIds, cancellationToken);

        var joinedGuildsByCharacter = allMemberships
            .GroupBy(m => m.CharacterId)
            .ToDictionary(g => g.Key, g => g.Select(m => m.GuildId).ToHashSet());

        var userGuilds = await userGuildsRepository.GetByUserDiscordIdAsync(query.RequesterDiscordId, cancellationToken);

        var result = new List<GuildEligibilityResponse>();

        foreach (var guildId in userGuilds.Select(ug => ug.GuildId))
        {
            var eligibility = await BuildGuildEligibilityAsync(
                guildId, characters, joinedGuildsByCharacter, query.RequesterDiscordId, cancellationToken);

            if (eligibility != null)
                result.Add(eligibility);
        }

        return Result<List<GuildEligibilityResponse>>.Ok(result);
    }

    /// <summary>
    /// Returns the guild's eligibility response (guild identity + its eligible characters), or
    /// <c>null</c> when the guild isn't usable at all (not registered, no active branch) or none
    /// of the requester's characters end up eligible on it.
    /// </summary>
    private async Task<GuildEligibilityResponse?> BuildGuildEligibilityAsync(
        string guildId,
        List<Character> characters,
        Dictionary<int, HashSet<string>> joinedGuildsByCharacter,
        string requesterDiscordId,
        CancellationToken cancellationToken)
    {
        var guild = await guildsRepository.GetByIdAsync(guildId, cancellationToken);
        if (guild == null || !guild.IsRegistered)
            return null;

        var activeBranches = await guildBranchesRepository.GetActiveForGuildAsync(guildId, cancellationToken);
        if (activeBranches.Count == 0)
            return null;

        var eligibleChars = characters
            .Where(character => IsCharacterEligible(character, guildId, activeBranches, joinedGuildsByCharacter, requesterDiscordId, cancellationToken))
            .Select(character => new EligibleCharacterDto
            {
                Id = character.Id,
                Name = character.Name,
                ClassId = character.ClassId,
                ClassName = character.Class.Name,
                ClassColor = $"#{character.Class.Color}",
            })
            .ToList();

        if (eligibleChars.Count == 0)
            return null;

        return new GuildEligibilityResponse
        {
            GuildId = guild.Id,
            GuildName = guild.Name,
            GuildIconHash = guild.IconHash,
            EligibleCharacters = eligibleChars,
        };
    }

    /// <summary>
    /// A character is eligible when it isn't already on this guild's roster, its WoW branch is
    /// active and roster-configured on this guild, and the requester passes that branch's
    /// roster-mode access check (open, or holds one of the configured Discord roles).
    /// </summary>
    private bool IsCharacterEligible(
        Character character,
        string guildId,
        List<GuildBranch> activeBranches,
        Dictionary<int, HashSet<string>> joinedGuildsByCharacter,
        string requesterDiscordId,
        CancellationToken cancellationToken)
    {
        if (joinedGuildsByCharacter.TryGetValue(character.Id, out var joined) && joined.Contains(guildId))
            return false;

        var branch = activeBranches.FirstOrDefault(b => b.BranchId == character.BranchId);
        if (branch == null || branch.RosterMode == null)
            return false;

        return branch.RosterMode == RosterMode.Open
            || DiscordRoleSetAccessHelper.HasAnyDiscordRole(discordBotService, guildId, branch.RosterRoleIds, requesterDiscordId, cancellationToken);
    }
}
