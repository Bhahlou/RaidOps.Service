using RaidOps.Domain.Models.Discord;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>
/// Repository contract for persisting and reading <see cref="GuildAuditLog"/> entries.
/// </summary>
public interface IGuildAuditLogRepository
{
    /// <summary>
    /// Inserts a new audit log entry.
    /// </summary>
    /// <param name="entry">The log entry to persist.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task AddAsync(GuildAuditLog entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the most recent audit log entries for a guild, ordered newest-first.
    /// </summary>
    /// <param name="guildId">Discord snowflake ID of the guild.</param>
    /// <param name="limit">Maximum number of entries to return.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task<List<GuildAuditLog>> GetRecentByGuildIdAsync(string guildId, int limit, CancellationToken cancellationToken = default);
}