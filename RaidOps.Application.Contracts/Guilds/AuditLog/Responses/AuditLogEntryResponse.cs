using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Guilds.AuditLog.Responses;

/// <summary>
/// A single entry of a guild's audit log, enriched with the actor's display info.
/// </summary>
public class AuditLogEntryResponse
{
    /// <summary>Primary key of the underlying log entry.</summary>
    public required int Id { get; set; }

    /// <summary>Discord snowflake ID of the user who triggered the action.</summary>
    public required string ActorDiscordId { get; set; }

    /// <summary>Discord username of the actor, or <c>null</c> if it could not be resolved.</summary>
    public string? ActorUsername { get; set; }

    /// <summary>Discord avatar hash of the actor, or <c>null</c> if it could not be resolved.</summary>
    public string? ActorAvatarHash { get; set; }

    /// <summary>Type of the action that was performed.</summary>
    public required GuildAuditAction ActionType { get; set; }

    /// <summary>Broad grouping over <see cref="ActionType"/>, for easier filtering/scanning.</summary>
    public required GuildAuditCategory Category { get; set; }

    /// <summary>
    /// Key-value pairs used by the front-end to interpolate the i18n template for <see cref="ActionType"/>.
    /// </summary>
    public Dictionary<string, string>? Variables { get; set; }

    /// <summary>UTC timestamp of when the action occurred.</summary>
    public required DateTime OccurredAt { get; set; }
}
