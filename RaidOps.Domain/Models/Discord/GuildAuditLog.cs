using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RaidOps.Domain.Enums;

namespace RaidOps.Domain.Models.Discord;

/// <summary>
/// An immutable log entry recording a notable action performed in a RaidOps guild.
/// Used to power the guild activity feed (e.g. "Bhahlou registered the guild", "Arthas joined the roster").
/// </summary>
[Table("GuildAuditLogs")]
public class GuildAuditLog
{
    /// <summary>Auto-incremented primary key.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>Discord snowflake ID of the guild this entry belongs to.</summary>
    [Required]
    public string GuildId { get; set; } = string.Empty;

    /// <summary>Discord snowflake ID of the user who triggered the action.</summary>
    [Required]
    public string ActorDiscordId { get; set; } = string.Empty;

    /// <summary>Category of the action that was performed.</summary>
    public GuildAuditAction ActionType { get; set; }

    /// <summary>
    /// Human-readable context for the action (e.g. "Character 'Arthas' joined the roster").
    /// Stored as a plain string; no structured schema enforced at the DB level.
    /// </summary>
    [MaxLength(512)]
    public string? Details { get; set; }

    /// <summary>UTC timestamp of when the action occurred.</summary>
    public DateTime OccurredAt { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The guild this log entry belongs to.</summary>
    public virtual Guild Guild { get; set; } = null!;
}
