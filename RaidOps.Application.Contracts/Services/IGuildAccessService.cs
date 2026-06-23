using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;

namespace RaidOps.Application.Contracts.Services;

/// <summary>
/// Computes a requester's <see cref="GuildAccessLevel"/> on a given guild — the single source of
/// truth replacing the ad hoc admin/role checks previously duplicated across guild-scoped handlers.
/// </summary>
public interface IGuildAccessService
{
    /// <summary>
    /// Returns the highest <see cref="GuildAccessLevel"/> the given Discord user holds on the
    /// specified guild. Fetches the membership/guild itself — prefer <see cref="ComputeAccessLevel"/>
    /// when the caller already has both loaded, to avoid a redundant round-trip.
    /// </summary>
    /// <param name="discordId">The Discord snowflake ID of the requester.</param>
    /// <param name="guildId">The Discord snowflake ID of the guild.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task<GuildAccessLevel> GetAccessLevelAsync(string discordId, string guildId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronous variant for callers that already have the <see cref="UserGuild"/> membership
    /// and its <see cref="Guild"/> loaded (e.g. projecting a whole guild list at once).
    /// </summary>
    /// <param name="membership">The requester's Discord-server membership for this guild.</param>
    /// <param name="guild">The guild itself.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation (the Discord role lookup is synchronous, but cancellation is still honored where checked).</param>
    GuildAccessLevel ComputeAccessLevel(UserGuild membership, Guild guild, CancellationToken cancellationToken = default);
}
