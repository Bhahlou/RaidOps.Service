namespace RaidOps.Application.Contracts.Guilds.AuditLog.Responses;

/// <summary>
/// A single page of a guild's audit log.
/// </summary>
public class GuildAuditLogPageResponse
{
    /// <summary>Entries for the requested page, newest-first.</summary>
    public required List<AuditLogEntryResponse> Entries { get; set; }

    /// <summary>Whether at least one more entry exists beyond this page.</summary>
    public required bool HasMore { get; set; }
}
