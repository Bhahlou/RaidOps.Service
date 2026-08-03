using System.ComponentModel.DataAnnotations.Schema;

namespace RaidOps.Domain.Models.Discord;

/// <summary>
/// Records that a user has acknowledged a specific front-end "what's new" changelog entry,
/// identified by its hand-curated, permanent id (e.g. "2026-08-02-raid-notifications"). The
/// changelog's content and grouping into themes/epochs live entirely in the front end and can be
/// reorganized freely — this table only tracks the per-user "seen" ledger, keyed on the entry id
/// (which never changes) rather than any group/epoch id (which can be renamed or restructured).
/// Composite primary key: (<see cref="UserDiscordId"/>, <see cref="EntryId"/>).
/// </summary>
[Table("SeenChangelogEntries")]
public class SeenChangelogEntry
{
    /// <summary>Discord snowflake ID of the user who acknowledged the entry.</summary>
    public string UserDiscordId { get; set; } = string.Empty;

    /// <summary>The front-end changelog entry id acknowledged.</summary>
    public string EntryId { get; set; } = string.Empty;

    /// <summary>UTC timestamp of when the entry was acknowledged.</summary>
    public DateTime SeenAt { get; set; }
}
