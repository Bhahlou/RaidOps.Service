using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RaidOps.Domain.Models.Reference;

/// <summary>
/// A World of Warcraft expansion (Classic, TBC, WotLK, …).
/// Static seeded reference table — never modified at runtime.
/// </summary>
[Table("Expansions")]
public class Expansion
{
    /// <summary>
    /// Internal sequential identifier (1 = Classic, 2 = TBC, …).
    /// Assigned at seed time; never auto-incremented.
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }

    /// <summary>Full display name, e.g. "The Burning Crusade".</summary>
    [Required, MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Short code for UI labels, e.g. "TBC", "WotLK".</summary>
    [Required, MaxLength(16)]
    public string ShortCode { get; set; } = string.Empty;

    /// <summary>Chronological release order (ascending). Drives display ordering.</summary>
    public int ReleaseOrder { get; set; }

    /// <summary>
    /// FK to the expansion this one forked from, for an expansion that isn't a straight
    /// continuation of the mainline Retail/Classic chronology (e.g. "Forever", a standalone new
    /// game branch that started from Classic content rather than continuing after the latest
    /// mainline expansion). Null for every mainline expansion.
    /// All of a forked branch's own expansions should point at the same root ancestor (not at each
    /// other) — <see cref="IsContentAvailableFrom"/> only follows one hop.
    /// </summary>
    public int? ForkedFromExpansionId { get; set; }

    /// <summary>The expansion this one forked from, if any (see <see cref="ForkedFromExpansionId"/>).</summary>
    public virtual Expansion? ForkedFromExpansion { get; set; }

    /// <summary>
    /// Whether content first introduced in <paramref name="origin"/> (e.g. a class's
    /// <c>FirstExpansionId</c>) should be considered available on this expansion.
    /// Mainline expansions behave as before — a plain chronological cutoff. A forked branch (this
    /// expansion has <see cref="ForkedFromExpansionId"/> set) additionally inherits everything from
    /// its fork point, but nothing from the mainline chronology beyond that point — the whole reason
    /// this exists: a class/race introduced on the mainline chronology after the fork point (e.g.
    /// Death Knight, added long after Classic) must not leak into a branch that forked off at Classic.
    /// </summary>
    public bool IsContentAvailableFrom(Expansion origin)
    {
        if (ForkedFromExpansionId == origin.ForkedFromExpansionId)
            return origin.ReleaseOrder <= ReleaseOrder;

        return ForkedFromExpansionId == origin.Id;
    }
}
