namespace RaidOps.Domain.Enums;

/// <summary>
/// How attendance is determined for a <c>RaidEvent</c>.
/// </summary>
public enum SignupMode
{
    /// <summary>
    /// No explicit signup: a member is assumed present unless they declared an absence via the
    /// existing availability system. This is the only mode implemented so far.
    /// </summary>
    DefaultPresent = 0,

    /// <summary>
    /// Members explicitly sign up (Accepted/Tentative/Declined) via a <c>RaidSignup</c>, mirrored
    /// by an interactive Discord planner message. Not implemented yet — this value exists so the
    /// field can be modeled now without a later schema change.
    /// </summary>
    Signup = 1,
}
