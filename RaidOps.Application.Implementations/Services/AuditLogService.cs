using System.Text.Json;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Services;

/// <summary>
/// Writes guild action entries to <see cref="IGuildAuditLogRepository"/>.
/// </summary>
public class AuditLogService(IGuildAuditLogRepository auditLogRepository) : IAuditLogService
{
    /// <inheritdoc/>
    public async Task LogAsync(
        string guildId,
        string actorDiscordId,
        GuildAuditAction action,
        Dictionary<string, string>? variables = null,
        CancellationToken cancellationToken = default)
    {
        var entry = new GuildAuditLog
        {
            GuildId = guildId,
            ActorDiscordId = actorDiscordId,
            ActionType = action,
            Details = variables != null ? JsonSerializer.Serialize(variables) : null,
            OccurredAt = DateTime.UtcNow,
        };

        await auditLogRepository.AddAsync(entry, cancellationToken);
    }
}
