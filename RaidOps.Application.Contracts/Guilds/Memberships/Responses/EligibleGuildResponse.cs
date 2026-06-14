namespace RaidOps.Application.Contracts.Guilds.Memberships.Responses;

/// <summary>
/// Represents a guild that a character can join (registered, configured, access granted, not yet a member).
/// Returned by <c>GetEligibleGuildsQuery</c>.
/// </summary>
public class EligibleGuildResponse
{
    /// <summary>Discord snowflake ID of the guild.</summary>
    public required string GuildId { get; set; }

    /// <summary>Name of the guild.</summary>
    public required string GuildName { get; set; }

    /// <summary>Discord icon hash of the guild, or <c>null</c> if no custom icon.</summary>
    public string? GuildIconHash { get; set; }
}
