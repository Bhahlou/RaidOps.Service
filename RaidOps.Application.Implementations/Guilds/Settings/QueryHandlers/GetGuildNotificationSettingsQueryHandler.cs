using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Settings.Queries;
using RaidOps.Application.Contracts.Guilds.Settings.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.Settings.QueryHandlers;

/// <summary>
/// Handles <see cref="GetGuildNotificationSettingsQuery"/> by verifying admin rights then
/// returning one row per <see cref="GuildNotificationEventType"/>, defaulting to disabled for
/// event types with no persisted row yet.
/// </summary>
public class GetGuildNotificationSettingsQueryHandler(
    IGuildAccessService guildAccessService,
    IGuildNotificationSettingsRepository notificationSettingsRepository)
    : IQueryHandlerAsync<GetGuildNotificationSettingsQuery, List<GuildNotificationSettingResponse>>
{
    /// <inheritdoc/>
    public async Task<Result<List<GuildNotificationSettingResponse>>> HandleAsync(GetGuildNotificationSettingsQuery query, CancellationToken cancellationToken)
    {
        var accessLevel = query.GuildBranchId != null
            ? await guildAccessService.GetAccessLevelAsync(query.RequesterDiscordId, query.GuildId, query.GuildBranchId.Value, cancellationToken)
            : await guildAccessService.GetAccessLevelAsync(query.RequesterDiscordId, query.GuildId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<List<GuildNotificationSettingResponse>>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild/branch.");

        var persisted = await notificationSettingsRepository.GetEffectiveForGuildAsync(query.GuildId, query.GuildBranchId, cancellationToken);
        var persistedByEventType = persisted.ToDictionary(s => s.EventType);

        var response = Enum.GetValues<GuildNotificationEventType>()
            .Select(eventType => persistedByEventType.TryGetValue(eventType, out var setting)
                ? new GuildNotificationSettingResponse { EventType = eventType, Enabled = setting.Enabled, ChannelId = setting.ChannelId, GuildBranchId = setting.GuildBranchId }
                : new GuildNotificationSettingResponse { EventType = eventType, Enabled = false, ChannelId = null, GuildBranchId = null })
            .ToList();

        return Result<List<GuildNotificationSettingResponse>>.Ok(response);
    }
}
