using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

/// <summary>
/// Provides read access to Discord guild data held in the bot's Gateway cache.
/// </summary>
public interface IGuildService
{
    /// <summary>
    /// Returns the cached <see cref="Guild"/> for the given guild ID.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the guild is not in the bot's cache.</exception>
    Guild Get(string guildId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all members of the given guild currently held in the bot's cache.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the guild is not in the bot's cache.</exception>
    IEnumerable<GuildUser> GetUsers(string guildId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the members of the given guild that hold the <see cref="Permissions.Administrator"/> permission.
    /// The guild owner is always included even if they hold no explicit admin role.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the guild is not in the bot's cache.</exception>
    IEnumerable<GuildUser> GetAdmins(string guildId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all non-managed, non-everyone roles for the given guild from the bot's Gateway cache.
    /// Managed roles (bot integrations) and the implicit <c>@everyone</c> role are excluded.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the guild is not in the bot's cache.</exception>
    IEnumerable<Role> GetRoles(string guildId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single member of the given guild from the bot's Gateway cache, or <c>null</c> if
    /// that user isn't a member (e.g. they left the server).
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the guild is not in the bot's cache.</exception>
    GuildUser? GetUser(string guildId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the text-postable channels (text and announcement channels) of the given guild from
    /// the bot's Gateway cache, each annotated with whether the bot currently has permission to
    /// post messages there.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the guild is not in the bot's cache.</exception>
    IEnumerable<DiscordChannelInfo> GetChannels(string guildId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the Discord-reported preferred locale of the given guild (e.g. <c>"en-US"</c>,
    /// <c>"fr"</c>), or <c>null</c> if Discord hasn't set one. Only meaningful for
    /// Community-enabled Discord servers — a regular guild reports a default value regardless of
    /// its members' actual language.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the guild is not in the bot's cache.</exception>
    string? GetPreferredLocale(string guildId, CancellationToken cancellationToken = default);
}
