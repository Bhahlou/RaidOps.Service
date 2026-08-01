namespace RaidOps.Application.Contracts.Raids.Lockout.Responses;

/// <summary>
/// The current weekly raid-lockout window for a guild branch, in the guild's local calendar dates.
/// Both fields are <c>null</c> when the branch has no <c>Region</c> configured yet — the caller
/// should fall back to its own default range in that case.
/// </summary>
public class GuildBranchLockoutWeekResponse
{
    /// <summary>First local calendar day of the current lockout week (the region's weekly reset day).</summary>
    public DateOnly? WeekStartLocal { get; set; }

    /// <summary>Last local calendar day of the current lockout week (the day before the next reset).</summary>
    public DateOnly? WeekEndLocal { get; set; }
}
