namespace RaidOps.Application.Contracts.Guilds.Memberships.Responses;

/// <summary>
/// A registered guild and the subset of the user's characters that are eligible to join it.
/// Returned by <c>GetEligibleGuildsBulkQuery</c>.
/// </summary>
public class GuildEligibilityResponse
{
    /// <summary>Discord snowflake ID of the guild.</summary>
    public required string GuildId { get; set; }

    /// <summary>Name of the guild.</summary>
    public required string GuildName { get; set; }

    /// <summary>Discord icon hash of the guild, or <c>null</c> if no custom icon.</summary>
    public string? GuildIconHash { get; set; }

    /// <summary>Characters the user owns that are eligible to join this guild.</summary>
    public required List<EligibleCharacterDto> EligibleCharacters { get; set; }
}
