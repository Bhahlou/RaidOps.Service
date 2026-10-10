namespace RaidOps.Domain.Enums;

/// <summary>
/// A member's response to a <c>RaidEvent</c> whose <see cref="SignupMode"/> is
/// <see cref="SignupMode.Signup"/>. The absence of a <c>RaidSignup</c> row for a given member is
/// itself meaningful ("no response yet") — there is deliberately no explicit member for it.
/// </summary>
public enum SignupStatus
{
    /// <summary>The member will attend — eligible for slot assignment.</summary>
    Accepted = 0,

    /// <summary>The member might attend, but hasn't committed — not eligible for slot assignment.</summary>
    Tentative = 1,

    /// <summary>The member will not attend — not eligible for slot assignment.</summary>
    Declined = 2,
}
