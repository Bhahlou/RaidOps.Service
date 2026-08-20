using NetCord;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

namespace RaidOps.Application.Implementations.Guilds.Access;

/// <summary>
/// Best-effort lookup of a guild member from the bot's Gateway cache, shared by every call site
/// that needs a member's guild-local nickname/avatar (audit log, roster, notification embeds).
/// </summary>
internal static class GuildMemberIdentityResolver
{
    /// <summary>
    /// Returns the member, or <c>null</c> if they're not found in the guild (left the server) or
    /// the bot isn't present there.
    /// </summary>
    internal static GuildUser? TryGetMember(IDiscordBotService discordBotService, string guildId, string discordId, CancellationToken cancellationToken)
    {
        try
        {
            return discordBotService.Guilds.GetUser(guildId, discordId, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
