using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Raids.Signups.Responses;

/// <summary>One roster member's response to a raid event — see <see cref="Queries.GetRaidSignupsQuery"/>. A member with no response yet is included with <see cref="Status"/> <c>null</c>.</summary>
public class RaidSignupResponse
{
    /// <summary>Discord snowflake ID of the member.</summary>
    public required string UserDiscordId { get; set; }

    /// <summary>Discord display name of the member, or <c>null</c> if it could not be resolved.</summary>
    public string? PlayerName { get; set; }

    /// <summary>The member's current response, or <c>null</c> if they haven't responded yet.</summary>
    public SignupStatus? Status { get; set; }

    /// <summary>UTC timestamp of when the response was last set, or <c>null</c> if they haven't responded yet.</summary>
    public DateTime? RespondedAtUtc { get; set; }

    /// <summary>The character being brought, set for <see cref="Status"/> Accepted and Tentative.</summary>
    public int? CharacterId { get; set; }

    /// <summary>Display name of <see cref="CharacterId"/>'s character, or <c>null</c> if not applicable.</summary>
    public string? CharacterName { get; set; }

    /// <summary>Blizzard class ID of <see cref="CharacterId"/>'s character, or <c>null</c> if not applicable — used to group Accepted responses by class.</summary>
    public int? ClassId { get; set; }

    /// <summary>Display name of the class, or <c>null</c> if not applicable.</summary>
    public string? ClassName { get; set; }

    /// <summary>The spec being brought, set for <see cref="Status"/> Accepted and Tentative.</summary>
    public int? SpecId { get; set; }

    /// <summary>Display name of <see cref="SpecId"/>'s spec, or <c>null</c> if not applicable.</summary>
    public string? SpecName { get; set; }

    /// <summary>Icon URL of <see cref="SpecId"/>'s spec, or <c>null</c> if not applicable.</summary>
    public string? SpecIconUrl { get; set; }
}
