using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;

namespace RaidOps.Application.Contracts.Services;

/// <summary>
/// Computes a requester's <see cref="GuildAccessLevel"/> on a given guild — the single source of
/// truth replacing the ad hoc admin/role checks previously duplicated across guild-scoped handlers.
/// Access is now branch-aware: <see cref="GuildBranch"/> carries its own roster/officer Discord
/// role sets, so most checks need to know which branch they're evaluating against.
/// </summary>
public interface IGuildAccessService
{
    /// <summary>
    /// Returns the highest <see cref="GuildAccessLevel"/> the given Discord user holds anywhere on
    /// the guild — <see cref="GuildAccessLevel.Officer"/> if they're a Discord admin, otherwise the
    /// max level across every active <see cref="GuildBranch"/> of the guild. Used by guild-wide
    /// operations that don't depend on a specific branch (guild identity settings, calendar
    /// availability, audit log, notification settings).
    /// </summary>
    /// <param name="discordId">The Discord snowflake ID of the requester.</param>
    /// <param name="guildId">The Discord snowflake ID of the guild.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task<GuildAccessLevel> GetAccessLevelAsync(string discordId, string guildId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the <see cref="GuildAccessLevel"/> the given Discord user holds on one specific,
    /// active guild branch. Used by branch-scoped operations (roster, joining, branch settings).
    /// Returns <see cref="GuildAccessLevel.None"/> if the branch doesn't exist, isn't active, or
    /// doesn't belong to <paramref name="guildId"/>.
    /// </summary>
    /// <param name="discordId">The Discord snowflake ID of the requester.</param>
    /// <param name="guildId">The Discord snowflake ID of the guild.</param>
    /// <param name="guildBranchId">The surrogate ID of the specific guild branch.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task<GuildAccessLevel> GetAccessLevelAsync(string discordId, string guildId, int guildBranchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronous variant for callers that already have the <see cref="UserGuild"/> membership
    /// and the specific <see cref="GuildBranch"/> loaded (e.g. projecting a whole guild list at
    /// once). Assumes the branch is active and its guild is registered — callers are responsible
    /// for that invariant.
    /// </summary>
    /// <param name="membership">The requester's Discord-server membership for this guild.</param>
    /// <param name="branch">The specific guild branch being evaluated.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation (the Discord role lookup is synchronous, but cancellation is still honored where checked).</param>
    GuildAccessLevel ComputeAccessLevel(UserGuild membership, GuildBranch branch, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <c>true</c> when <paramref name="requesterDiscordId"/> outranks
    /// <paramref name="targetDiscordId"/> on the given guild branch — i.e. is allowed to take
    /// hierarchy-sensitive actions on them (e.g. excluding them from the roster). The guild owner
    /// (<see cref="UserGuild.IsOwner"/>) is never outranked, not even by another Discord admin.
    /// Discord admins otherwise always outrank everyone. Branch officers (via
    /// <see cref="GuildBranch.OfficerRoleIds"/>) outrank branch non-officers. Between two peers
    /// (both or neither officer), the comparison falls back to each user's highest-positioned
    /// Discord role — a relative ranking between two known people, not the broken hierarchy-
    /// threshold mechanism the role sets replaced.
    /// </summary>
    /// <param name="guildId">The Discord snowflake ID of the guild.</param>
    /// <param name="guildBranchId">The surrogate ID of the guild branch the action is scoped to.</param>
    /// <param name="requesterDiscordId">The Discord snowflake ID of the user taking the action.</param>
    /// <param name="targetDiscordId">The Discord snowflake ID of the user being acted upon.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task<bool> OutranksAsync(string guildId, int guildBranchId, string requesterDiscordId, string targetDiscordId, CancellationToken cancellationToken = default);
}
