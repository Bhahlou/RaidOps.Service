namespace RaidOps.Application.Contracts.Raids.Signups.Responses;

/// <summary>One raid-viable spec declared on a <see cref="RaidSignupCharacterResponse"/>'s character.</summary>
public class RaidSignupSpecResponse
{
    /// <summary>Internal spec ID.</summary>
    public required int SpecId { get; set; }

    /// <summary>Spec display name.</summary>
    public required string SpecName { get; set; }

    /// <summary>Whether this is the character's main raid spec.</summary>
    public required bool IsMain { get; set; }
}
