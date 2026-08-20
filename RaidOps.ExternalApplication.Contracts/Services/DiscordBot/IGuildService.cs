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
    /// Returns the categories of the given guild from the bot's Gateway cache, ordered the same way
    /// Discord's own channel list sorts them, each annotated with whether the bot can create a
    /// channel there — lets an officer pick where a bot-created channel (see
    /// <see cref="CreateTextChannelAsync"/>) should live without hitting a 403 from Discord.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the guild is not in the bot's cache.</exception>
    DiscordCategoriesInfo GetCategories(string guildId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new text channel in the given guild via the bot's REST client (unlike every other
    /// method here, this makes a live Discord API call rather than reading the Gateway cache) and
    /// returns it in the same shape <see cref="GetChannels"/> uses. Throws if the bot lacks the
    /// Manage Channels permission — callers should catch and surface a friendly failure rather than
    /// treating it as fatal, since channel creation is always an optional convenience over picking
    /// an existing channel. <paramref name="categoryId"/> nests it under that category when set.
    /// </summary>
    Task<DiscordChannelInfo> CreateTextChannelAsync(string guildId, string name, string? categoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a channel via the bot's REST client — used to clean up a channel RaidOps created
    /// specifically for a raid event when that event is deleted. Throws if the bot lacks permission
    /// or the channel no longer exists; callers should treat this as best-effort cleanup rather than
    /// fatal, since the event itself is already gone either way.
    /// </summary>
    Task DeleteChannelAsync(string channelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the Discord-reported preferred locale of the given guild (e.g. <c>"en-US"</c>,
    /// <c>"fr"</c>), or <c>null</c> if Discord hasn't set one. Only meaningful for
    /// Community-enabled Discord servers — a regular guild reports a default value regardless of
    /// its members' actual language.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the guild is not in the bot's cache.</exception>
    string? GetPreferredLocale(string guildId, CancellationToken cancellationToken = default);
}
