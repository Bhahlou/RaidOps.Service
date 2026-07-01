using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

namespace RaidOps.Application.Implementations.Guilds.Memberships.Helpers;

/// <summary>
/// Shared helper for evaluating Discord role-based roster access across query handlers.
/// </summary>
internal static class DiscordRosterAccessHelper
{
    /// <summary>
    /// Returns <c>true</c> when the user holds at least one Discord role whose position is
    /// greater than or equal to <paramref name="minRosterRoleId"/> in the given guild.
    /// Returns <c>false</c> when the bot is not present in the guild (<see cref="InvalidOperationException"/>),
    /// the minimum role is not found, or the user is not a member.
    /// </summary>
    internal static bool HasDiscordRoleAccess(
        IDiscordBotService discordBotService,
        string guildId,
        string minRosterRoleId,
        string requesterDiscordId,
        CancellationToken cancellationToken)
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
            return false;
        }
    }
}
