using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.Access;

/// <summary>
/// Default <see cref="IGuildAccessService"/> implementation. Single source of truth for the
/// Public/Roster/Officer hierarchy, replacing the admin and Discord-role checks that were
/// previously duplicated across guild-scoped handlers.
/// </summary>
public class GuildAccessService(
    IUserGuildsRepository userGuildsRepository,
    IGuildsRepository guildsRepository,
    IDiscordBotService discordBotService) : IGuildAccessService
{
    /// <inheritdoc/>
    public async Task<GuildAccessLevel> GetAccessLevelAsync(string discordId, string guildId, CancellationToken cancellationToken = default)
    {
        var userGuilds = await userGuildsRepository.GetByUserDiscordIdAsync(discordId, cancellationToken);
        var membership = userGuilds.FirstOrDefault(ug => ug.GuildId == guildId);
        if (membership == null)
            return GuildAccessLevel.None;

        if (membership.IsAdmin)
            return GuildAccessLevel.Officer;

        var guild = await guildsRepository.GetByIdAsync(guildId, cancellationToken);
        if (guild == null || !guild.IsRegistered)
            return GuildAccessLevel.None;

        return ComputeAccessLevel(membership, guild, cancellationToken);
    }

    /// <inheritdoc/>
    public GuildAccessLevel ComputeAccessLevel(UserGuild membership, Guild guild, CancellationToken cancellationToken = default)
    {
        if (membership.IsAdmin)
            return GuildAccessLevel.Officer;

        if (!guild.IsRegistered)
            return GuildAccessLevel.None;

        if (HasRequiredDiscordRole(guild.Id, guild.MinOfficerRoleId, membership.UserDiscordId, cancellationToken))
            return GuildAccessLevel.Officer;

        var hasRosterAccess = guild.RosterMode switch
        {
            RosterMode.Open => true,
            RosterMode.DiscordRoleOnly => HasRequiredDiscordRole(guild.Id, guild.MinRosterRoleId, membership.UserDiscordId, cancellationToken),
            _ => false,
        };

        return hasRosterAccess ? GuildAccessLevel.Roster : GuildAccessLevel.Public;
    }

    /// <summary>
    /// Checks whether the requester holds a Discord role at or above <paramref name="minRoleId"/>'s
    /// position. Used for both the roster threshold and the Officer threshold — same "this role or
    /// anything higher in the hierarchy" semantics either way. Silently denies access if the bot
    /// isn't in the guild or the role/member can't be found — callers that need to distinguish
    /// those failure modes should not rely on this method.
    /// </summary>
    private bool HasRequiredDiscordRole(string guildId, string? minRoleId, string discordId, CancellationToken cancellationToken)
    {
        if (minRoleId == null)
            return false;

        try
        {
            var roles = discordBotService.Guilds.GetRoles(guildId, cancellationToken)
                .ToDictionary(r => r.Id.ToString());

            if (!roles.TryGetValue(minRoleId, out var minRole))
                return false;

            var guildUser = discordBotService.Guilds.GetUsers(guildId, cancellationToken)
                .FirstOrDefault(u => u.Id.ToString() == discordId);

            if (guildUser == null)
                return false;

            return guildUser.RoleIds.Any(rid =>
                roles.TryGetValue(rid.ToString(), out var role) && role.Position >= minRole.Position);
        }
        catch (InvalidOperationException)
        {
            // Bot not in this guild — no threshold-based access to grant.
            return false;
        }
    }
}
