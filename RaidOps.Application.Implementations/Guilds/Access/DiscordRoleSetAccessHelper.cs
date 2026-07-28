using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

namespace RaidOps.Application.Implementations.Guilds.Access;

/// <summary>
/// Shared helper checking whether a Discord user holds at least one role from an explicit role-ID
/// set — the "holds any of these roles" replacement for the old single-role, hierarchy-position
/// threshold (<c>HasRequiredDiscordRole</c>). Position-based "this role or anything above it"
/// breaks down once two branches' roles sit at unrelated positions on the same flat Discord axis,
/// so branch role checks are now plain set membership.
/// </summary>
internal static class DiscordRoleSetAccessHelper
{
    /// <summary>
    /// Returns <c>true</c> when <paramref name="discordId"/> holds at least one Discord role whose
    /// ID is in <paramref name="roleIds"/>, within <paramref name="guildId"/>. Returns <c>false</c>
    /// when <paramref name="roleIds"/> is empty (not yet configured), the bot isn't present in the
    /// guild (<see cref="InvalidOperationException"/>), or the user isn't found as a guild member.
    /// </summary>
    internal static bool HasAnyDiscordRole(
        IDiscordBotService discordBotService,
        string guildId,
        IReadOnlyCollection<string> roleIds,
        string discordId,
        CancellationToken cancellationToken)
    {
        if (roleIds.Count == 0)
            return false;

        try
        {
            var guildUser = discordBotService.Guilds.GetUsers(guildId, cancellationToken)
                .FirstOrDefault(u => u.Id.ToString() == discordId);

            if (guildUser == null)
                return false;

            var heldRoleIds = guildUser.RoleIds.Select(r => r.ToString()).ToHashSet();
            return roleIds.Any(heldRoleIds.Contains);
        }
        catch (InvalidOperationException)
        {
            // Bot not in this guild — no role-set access to grant.
            return false;
        }
    }
}
