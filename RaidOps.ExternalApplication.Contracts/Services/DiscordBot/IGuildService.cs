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
}
