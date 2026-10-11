using System.ComponentModel.DataAnnotations.Schema;

namespace RaidOps.Domain.Models.Discord;

/// <summary>
/// Records that a user has completed or dismissed a front-end guided tour, identified by its
/// hand-curated, permanent id (e.g. "general", "admin"). The tour's steps and copy live entirely
/// in the front end — this table only tracks the per-user "seen" ledger so a tour isn't replayed
/// on every device. Composite primary key: (<see cref="UserDiscordId"/>, <see cref="TourId"/>).
/// </summary>
[Table("SeenTours")]
public class SeenTour
{
    /// <summary>Discord snowflake ID of the user who finished or skipped the tour.</summary>
    public string UserDiscordId { get; set; } = string.Empty;

    /// <summary>The front-end tour id acknowledged.</summary>
    public string TourId { get; set; } = string.Empty;

    /// <summary>UTC timestamp of when the tour was acknowledged.</summary>
    public DateTime SeenAt { get; set; }
}
