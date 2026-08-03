using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

namespace RaidOps.Application.Implementations.Notifications.Helpers;

/// <summary>
/// Resolves the acting member's guild nickname + avatar into a <see cref="DiscordEmbedAuthor"/>
/// byline, shared by every notification-content builder (absences, raids, ...) that stamps one on
/// its embeds.
/// </summary>
internal static class DiscordEmbedAuthorResolver
{
    /// <summary>
    /// Best-effort — a member not found in the bot's cache (left the server, bot not present)
    /// just means no author byline, never a failure to notify.
    /// </summary>
    public static DiscordEmbedAuthor? Resolve(IDiscordBotService discordBotService, string guildId, string requesterDiscordId, CancellationToken cancellationToken)
    {
        try
        {
            var member = discordBotService.Guilds.GetUser(guildId, requesterDiscordId, cancellationToken);
            if (member is null)
                return null;

            var name = member.Nickname ?? member.GlobalName ?? member.Username;
            var iconUrl = (member.HasGuildAvatar ? member.GetGuildAvatarUrl() : member.GetAvatarUrl())?.ToString();
            return new DiscordEmbedAuthor(name, iconUrl);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
