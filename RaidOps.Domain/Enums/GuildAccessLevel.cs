namespace RaidOps.Domain.Enums;

/// <summary>
/// The level of access a Discord user holds on a given guild, from least to most privileged.
/// Computed fresh per request by <c>IGuildAccessService</c> — never cached long-term, mirroring
/// how Discord roles themselves are re-fetched on each connection.
/// </summary>
public enum GuildAccessLevel
{
    /// <summary>Not a Discord member of this guild's server, or the guild isn't registered.</summary>
    None = 0,

    /// <summary>A Discord member of a registered guild's server, regardless of roster eligibility.</summary>
    Public = 1,

    /// <summary>Satisfies the guild's roster settings (open mode, or holds the required Discord role).</summary>
    Roster = 2,

    /// <summary>Holds the Administrator permission on the guild's Discord server.</summary>
    Officer = 3,
}
