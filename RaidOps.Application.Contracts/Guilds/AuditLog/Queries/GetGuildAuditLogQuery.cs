using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.AuditLog.Responses;
using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Guilds.AuditLog.Queries;

/// <summary>
/// Query that returns a page of audit log entries for a registered guild.
/// The requesting user must be an admin of the target guild.
/// </summary>
public class GetGuildAuditLogQuery : IQueryRequest<GuildAuditLogPageResponse>
{
    /// <summary>The Discord snowflake ID of the guild whose audit log to retrieve.</summary>
    public required string GuildId { get; set; }

    /// <summary>The Discord snowflake ID of the user requesting the audit log.</summary>
    public required string RequesterDiscordId { get; set; }

    /// <summary>1-based page number.</summary>
    public int Page { get; set; } = 1;

    /// <summary>Maximum number of entries per page.</summary>
    public int PageSize { get; set; } = 25;

    /// <summary>When set, only entries of this action type are returned.</summary>
    public GuildAuditAction? ActionType { get; set; }

    /// <summary>
    /// When set, only entries whose action type belongs to this category are returned.
    /// Ignored if <see cref="ActionType"/> is also set.
    /// </summary>
    public GuildAuditCategory? Category { get; set; }
}
