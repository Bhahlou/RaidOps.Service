using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Memberships.Queries;
using RaidOps.Application.Contracts.Guilds.Memberships.Responses;
using RaidOps.Application.Implementations.Guilds.Memberships.Helpers;
using RaidOps.Domain.Enums;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.Memberships.QueryHandlers;

/// <summary>
/// Handles <see cref="GetEligibleGuildsBulkQuery"/> by returning, for each registered guild the
/// user belongs to on Discord, the subset of their active characters that are not yet on that
/// guild's roster and pass the roster-mode access check.
/// All characters and memberships are fetched in bulk to avoid N+1 queries.
/// </summary>
public class GetEligibleGuildsBulkQueryHandler(
    ICharacterRepository characterRepository,
    IGuildsRepository guildsRepository,
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
            var guild = await guildsRepository.GetByIdAsync(guildId, cancellationToken);
            if (guild == null || !guild.IsRegistered || guild.RosterMode == null)
                continue;

            var isAccessGranted = guild.RosterMode == RosterMode.Open
                || (guild.MinRosterRoleId != null && DiscordRosterAccessHelper.HasDiscordRoleAccess(discordBotService, guild.Id, guild.MinRosterRoleId, query.RequesterDiscordId, cancellationToken));

            if (!isAccessGranted)
                continue;

            var eligibleChars = characters
                .Where(c => !joinedGuildsByCharacter.TryGetValue(c.Id, out var joined) || !joined.Contains(guildId))
                .Select(c => new EligibleCharacterDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    ClassId = c.ClassId,
                    ClassName = c.Class.Name,
                    ClassColor = $"#{c.Class.Color}",
                })
                .ToList();

            if (eligibleChars.Count == 0)
                continue;

            result.Add(new GuildEligibilityResponse
            {
                GuildId = guild.Id,
                GuildName = guild.Name,
                GuildIconHash = guild.IconHash,
                EligibleCharacters = eligibleChars,
            });
        }

        return Result<List<GuildEligibilityResponse>>.Ok(result);
    }
}
