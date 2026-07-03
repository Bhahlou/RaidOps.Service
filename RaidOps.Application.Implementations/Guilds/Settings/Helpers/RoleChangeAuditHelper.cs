using NetCord;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

namespace RaidOps.Application.Implementations.Guilds.Settings.Helpers;

/// <summary>
/// Shared audit-log helpers for command handlers that log a Discord role threshold change
/// (roster access threshold, Officer access threshold). Resolving a role ID into a human-readable
/// name/color/icon for the audit log is identical regardless of which threshold changed.
/// </summary>
internal static class RoleChangeAuditHelper
{
    /// <summary>
    /// Fetches the guild's Discord roles from the bot's Gateway cache, or null if the bot isn't
    /// in the guild — callers fall back to a placeholder rather than failing the update.
    /// </summary>
    public static List<Role>? TryGetRoles(IDiscordBotService discordBotService, string guildId, CancellationToken cancellationToken)
    {
        try
        {
            return [.. discordBotService.Guilds.GetRoles(guildId, cancellationToken)];
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Resolves a Discord role's display info (name, color, icon) and adds it to
    /// <paramref name="variables"/> under <paramref name="prefix"/> + <paramref name="fieldName"/> —
    /// a raw role ID means nothing to a human reading the audit log.
    /// </summary>
    public static void AddRoleVariables(
        Dictionary<string, string> variables, string prefix, string fieldName, List<Role>? roles, string roleId)
    {
        var role = roles?.FirstOrDefault(r => r.Id.ToString() == roleId);
        if (role == null)
            return;

        variables[$"{prefix}{fieldName}Name"] = role.Name;

        var color = role.Colors?.PrimaryColor.RawValue ?? 0;
        if (color != 0)
            variables[$"{prefix}{fieldName}Color"] = color.ToString();

        // Full CDN URL (not just the hash) so the front end never needs the role ID at all.
        if (role.IconHash != null)
            variables[$"{prefix}{fieldName}IconUrl"] = $"https://cdn.discordapp.com/role-icons/{role.Id}/{role.IconHash}.webp?size=32";
    }
}
