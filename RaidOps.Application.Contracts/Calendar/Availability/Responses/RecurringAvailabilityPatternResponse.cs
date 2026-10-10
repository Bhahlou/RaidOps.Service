namespace RaidOps.Application.Contracts.Calendar.Availability.Responses;

/// <summary>
/// DTO representing a recurring availability pattern, as returned for editing.
/// </summary>
public class RecurringAvailabilityPatternResponse
{
    /// <summary>The pattern's identifier.</summary>
    public int Id { get; set; }

    /// <summary>The guild of this pattern's branch scope, or <c>null</c> if it's Global.</summary>
    public string? GuildId { get; set; }

    /// <summary>This pattern's specific branch scope, or <c>null</c> if it's Global.</summary>
    public int? GuildBranchId { get; set; }

    /// <summary>Optional friendly name for the member's own reference.</summary>
    public string? Label { get; set; }

    /// <summary>Length of the recurrence cycle in days.</summary>
    public int CycleLengthDays { get; set; }

    /// <summary>Reference date at which offset 0 of the cycle begins.</summary>
    public DateOnly AnchorDate { get; set; }

    /// <summary>The days within the cycle that are not fully available.</summary>
    public List<RecurringAvailabilityPatternDayResponse> Days { get; set; } = [];
}
