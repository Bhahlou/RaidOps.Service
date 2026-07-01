namespace RaidOps.Application.Contracts.Guilds.Memberships.Responses;

/// <summary>
/// A character eligible to join a specific guild.
/// Embedded in <see cref="GuildEligibilityResponse"/> returned by the bulk eligibility query.
/// </summary>
public class EligibleCharacterDto
{
    /// <summary>Internal RaidOps character identifier.</summary>
    public required int Id { get; set; }

    /// <summary>Character name (e.g. "Arthas").</summary>
    public required string Name { get; set; }

    /// <summary>Blizzard class ID.</summary>
    public required int ClassId { get; set; }

    /// <summary>Class display name (e.g. "Death Knight").</summary>
    public required string ClassName { get; set; }

    /// <summary>Official class colour as a #RRGGBB hex string (e.g. "#C41E3A").</summary>
    public required string ClassColor { get; set; }
}
