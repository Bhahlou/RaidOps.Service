using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Infrastructure.Persistence.Implementations.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IGuildAuditLogRepository"/>.
/// </summary>
public class GuildAuditLogRepository(RaidOpsDbContext context) : IGuildAuditLogRepository
{
    /// <summary>
    /// Inserts a new audit log entry and saves changes.
    /// </summary>
    public async Task AddAsync(GuildAuditLog entry, CancellationToken cancellationToken = default)
    {
        context.GuildAuditLogs.Add(entry);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Returns the <paramref name="limit"/> most recent log entries for a guild, newest-first.
    /// </summary>
    public async Task<List<GuildAuditLog>> GetRecentByGuildIdAsync(string guildId, int limit, CancellationToken cancellationToken = default)
        => await context.GuildAuditLogs
            .Where(l => l.GuildId == guildId)
            .OrderByDescending(l => l.OccurredAt)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Returns a page of log entries for a guild, newest-first, optionally filtered to a set of action types.
    /// </summary>
    public async Task<List<GuildAuditLog>> GetPagedByGuildIdAsync(
        string guildId, int skip, int take, IReadOnlyCollection<GuildAuditAction>? actionTypes, CancellationToken cancellationToken = default)
    {
        var query = context.GuildAuditLogs.Where(l => l.GuildId == guildId);

        if (actionTypes is { Count: > 0 })
            query = query.Where(l => actionTypes.Contains(l.ActionType));

        return await query
            .OrderByDescending(l => l.OccurredAt)
            .Skip(skip)
            .Take(take)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
