using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Memberships.Queries;
using RaidOps.Application.Contracts.Guilds.Memberships.Responses;
using RaidOps.Domain.Enums;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.Memberships.QueryHandlers;

/// <summary>
/// Handles <see cref="GetEligibleGuildsQuery"/> by returning registered guilds that the
/// character can join: the user is a Discord member, the guild is configured, the character
/// is not already a member, and the roster mode grants access.
/// </summary>
public class GetEligibleGuildsQueryHandler(
    ICharacterRepository characterRepository,
    IGuildsRepository guildsRepository,
    IUserGuildsRepository userGuildsRepository,
    IGuildMembershipRepository membershipRepository,
    IDiscordBotService discordBotService) : IQueryHandlerAsync<GetEligibleGuildsQuery, List<EligibleGuildResponse>>
{
    /// <inheritdoc/>
    public async Task<Result<List<EligibleGuildResponse>>> HandleAsync(GetEligibleGuildsQuery query, CancellationToken cancellationToken)
    {
        var character = await characterRepository.GetByIdAsync(query.CharacterId, cancellationToken);
        if (character == null)
            return Result<List<EligibleGuildResponse>>.Fail(ResponseDetail.CharacterNotFound, $"Character '{query.CharacterId}' does not exist.");

        if (character.UserDiscordId != query.RequesterDiscordId)
            return Result<List<EligibleGuildResponse>>.Fail(ResponseDetail.CharacterNotOwned, "You do not own this character.");

        // Guilds the user belongs to on Discord
        var userGuilds = await userGuildsRepository.GetByUserDiscordIdAsync(query.RequesterDiscordId, cancellationToken);

        // Guilds the character is already on
        var existingMemberships = await membershipRepository.GetByCharacterIdAsync(query.CharacterId, cancellationToken);
        var alreadyJoinedIds = existingMemberships.Select(m => m.GuildId).ToHashSet();

        var eligible = new List<EligibleGuildResponse>();

        foreach (var guildId in userGuilds.Select(ug => ug.GuildId))
        {
            if (alreadyJoinedIds.Contains(guildId))
                continue;

            var guild = await guildsRepository.GetByIdAsync(guildId, cancellationToken);
            if (guild == null || !guild.IsRegistered || guild.RosterMode == null)
                continue;

            var isEligible = guild.RosterMode == RosterMode.Open
                || (guild.MinRosterRoleId != null && HasDiscordRoleAccess(guild.Id, guild.MinRosterRoleId, query.RequesterDiscordId, cancellationToken));

            if (isEligible)
            {
                eligible.Add(new EligibleGuildResponse
                {
                    GuildId = guild.Id,
                    GuildName = guild.Name,
                    GuildIconHash = guild.IconHash,
                });
            }
        }

        return Result<List<EligibleGuildResponse>>.Ok(eligible);
    }

    private bool HasDiscordRoleAccess(string guildId, string minRosterRoleId, string requesterDiscordId, CancellationToken cancellationToken)
    {
        try
        {
            var roles = discordBotService.Guilds.GetRoles(guildId, cancellationToken)
                .ToDictionary(r => r.Id.ToString());

            if (!roles.TryGetValue(minRosterRoleId, out var minRole))
                return false;

            var guildUser = discordBotService.Guilds.GetUsers(guildId, cancellationToken)
                .FirstOrDefault(u => u.Id.ToString() == requesterDiscordId);

            if (guildUser == null)
                return false;

            return guildUser.RoleIds.Any(rid =>
                roles.TryGetValue(rid.ToString(), out var role) && role.Position >= minRole.Position);
        }
        catch (InvalidOperationException)
        {
            // Bot not in this guild — skip silently
            return false;
        }
    }
}
