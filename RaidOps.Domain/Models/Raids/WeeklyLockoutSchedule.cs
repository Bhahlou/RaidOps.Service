using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RaidOps.Domain.Models.Raids;

/// <summary>
/// The weekly raid-lockout reset schedule for one Blizzard API region ("eu", "us", "kr", "tw") —
/// static seeded reference data, same convention as <see cref="RaidZone"/>. The weekly reset is a
/// single fixed UTC instant shared by every branch/expansion in that region (Retail and every
/// Classic variant reset together), so this lives once per region rather than being duplicated
/// across every <see cref="RaidZone"/> row. A <see cref="Discord.GuildBranch"/> resolves its
/// applicable schedule via <see cref="Discord.GuildBranch.Region"/>.
/// </summary>
[Table("WeeklyLockoutSchedules")]
public class WeeklyLockoutSchedule
{
    /// <summary>Blizzard API region code ("eu", "us", "kr", "tw"). Primary key.</summary>
    [Key, MaxLength(4)]
    public string Region { get; set; } = string.Empty;

    /// <summary>
    /// A genuine past weekly-reset instant (UTC) for this region — the origin the lockout engine
    /// advances from in whole-cadence jumps. Any correct instant works; only its weekday and
    /// time-of-day matter, not how far in the past it is.
    /// </summary>
    public DateTime AnchorUtc { get; set; }

    /// <summary>Days between weekly resets — always 7 today, kept as data rather than a constant so the engine's (anchor, cadence) shape stays uniform with per-zone overrides.</summary>
    public int CadenceDays { get; set; } = 7;
}
