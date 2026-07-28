using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Infrastructure.Persistence.Implementations.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IGuildNotificationSettingsRepository"/>.
/// </summary>
public class GuildNotificationSettingsRepository(RaidOpsDbContext context) : IGuildNotificationSettingsRepository
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<GuildNotificationSetting>> GetAllForGuildAsync(string guildId, CancellationToken cancellationToken = default)
        => await context.GuildNotificationSettings
            .Where(s => s.GuildId == guildId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<GuildNotificationSetting?> GetAsync(string guildId, GuildNotificationEventType eventType, int? guildBranchId, CancellationToken cancellationToken = default)
    {
        if (guildBranchId != null)
        {
            var branchSetting = await context.GuildNotificationSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.GuildId == guildId && s.EventType == eventType && s.GuildBranchId == guildBranchId, cancellationToken);
            if (branchSetting != null)
                return branchSetting;
        }

        return await context.GuildNotificationSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.GuildId == guildId && s.EventType == eventType && s.GuildBranchId == null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task UpsertRangeAsync(string guildId, IEnumerable<GuildNotificationSetting> settings, CancellationToken cancellationToken = default)
    {
        var incoming = settings.ToList();
        var eventTypes = incoming.Select(s => s.EventType).ToList();

        var existing = await context.GuildNotificationSettings
            .Where(s => s.GuildId == guildId && eventTypes.Contains(s.EventType))
            .ToDictionaryAsync(s => s.EventType, cancellationToken);

        foreach (var setting in incoming)
        {
            if (existing.TryGetValue(setting.EventType, out var existingSetting))
            {
                existingSetting.Enabled = setting.Enabled;
                existingSetting.ChannelId = setting.ChannelId;
            }
            else
            {
                context.GuildNotificationSettings.Add(new GuildNotificationSetting
                {
                    GuildId = guildId,
                    EventType = setting.EventType,
                    Enabled = setting.Enabled,
                    ChannelId = setting.ChannelId,
                });
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
