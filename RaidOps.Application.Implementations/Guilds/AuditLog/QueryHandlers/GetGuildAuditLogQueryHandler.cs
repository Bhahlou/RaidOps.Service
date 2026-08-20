using System.Text.Json;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.AuditLog.Queries;
using RaidOps.Application.Contracts.Guilds.AuditLog.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Guilds.Access;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.AuditLog.QueryHandlers;

/// <summary>
/// Handles <see cref="GetGuildAuditLogQuery"/> by verifying admin rights then returning a page
/// of the guild's audit log, enriched with actor display info.
/// </summary>
public class GetGuildAuditLogQueryHandler(
    IGuildAccessService guildAccessService,
    IGuildAuditLogRepository auditLogRepository,
    IUsersRepository usersRepository,
    IDiscordBotService discordBotService) : IQueryHandlerAsync<GetGuildAuditLogQuery, GuildAuditLogPageResponse>
{
    /// <summary>
    /// Single source of truth for which <see cref="GuildAuditCategory"/> each action belongs to,
    /// so the front end never needs its own copy of this mapping.
    /// </summary>
    private static readonly Dictionary<GuildAuditAction, GuildAuditCategory> CategoryByAction = new()
    {
        [GuildAuditAction.GuildRegistered] = GuildAuditCategory.Guild,
        [GuildAuditAction.SettingsUpdated] = GuildAuditCategory.Settings,
        [GuildAuditAction.MemberJoined] = GuildAuditCategory.Roster,
        [GuildAuditAction.MemberLeft] = GuildAuditCategory.Roster,
        [GuildAuditAction.MemberExcluded] = GuildAuditCategory.Roster,
        [GuildAuditAction.MemberRankUpdated] = GuildAuditCategory.Roster,
        [GuildAuditAction.OfficerThresholdUpdated] = GuildAuditCategory.Settings,
        [GuildAuditAction.AvailabilityExceptionDeclared] = GuildAuditCategory.Availability,
        [GuildAuditAction.AvailabilityExceptionDeleted] = GuildAuditCategory.Availability,
        [GuildAuditAction.RecurringAvailabilityPatternCreated] = GuildAuditCategory.Availability,
        [GuildAuditAction.RecurringAvailabilityPatternUpdated] = GuildAuditCategory.Availability,
        [GuildAuditAction.RecurringAvailabilityPatternStopped] = GuildAuditCategory.Availability,
        [GuildAuditAction.NotificationSettingsUpdated] = GuildAuditCategory.Settings,
        [GuildAuditAction.BranchActivated] = GuildAuditCategory.Branches,
        [GuildAuditAction.BranchDeactivated] = GuildAuditCategory.Branches,
        [GuildAuditAction.BranchRosterSettingsUpdated] = GuildAuditCategory.Branches,
        [GuildAuditAction.NotificationSettingsReset] = GuildAuditCategory.Settings,
        [GuildAuditAction.RaidSeriesCreated] = GuildAuditCategory.Raids,
        [GuildAuditAction.RaidSeriesUpdated] = GuildAuditCategory.Raids,
        [GuildAuditAction.RaidSeriesDeactivated] = GuildAuditCategory.Raids,
        [GuildAuditAction.RaidEventCreated] = GuildAuditCategory.Raids,
        [GuildAuditAction.RaidEventUpdated] = GuildAuditCategory.Raids,
        [GuildAuditAction.RaidEventCancelled] = GuildAuditCategory.Raids,
        [GuildAuditAction.RaidEventDeleted] = GuildAuditCategory.Raids,
        [GuildAuditAction.RaidEventPublished] = GuildAuditCategory.Raids,
        [GuildAuditAction.BranchRegionUpdated] = GuildAuditCategory.Branches,
        [GuildAuditAction.SlotAssigned] = GuildAuditCategory.Raids,
        [GuildAuditAction.SlotUnassigned] = GuildAuditCategory.Raids,
        [GuildAuditAction.SlotsSwapped] = GuildAuditCategory.Raids,
        [GuildAuditAction.SlotAssignmentSpecChanged] = GuildAuditCategory.Raids,
        [GuildAuditAction.BranchSignupModeUpdated] = GuildAuditCategory.Branches,
    };

    /// <inheritdoc/>
    public async Task<Result<GuildAuditLogPageResponse>> HandleAsync(GetGuildAuditLogQuery query, CancellationToken cancellationToken)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(query.RequesterDiscordId, query.GuildId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<GuildAuditLogPageResponse>.Fail(ResponseDetail.Forbidden, "User is not an admin of this guild.");

        var actionTypes = ResolveActionTypeFilter(query);

        var skip = (query.Page - 1) * query.PageSize;
        var fetched = await auditLogRepository.GetPagedByGuildIdAsync(
            query.GuildId, skip, query.PageSize + 1, actionTypes, cancellationToken);

        var hasMore = fetched.Count > query.PageSize;
        var page = fetched.Take(query.PageSize).ToList();

        var actorIds = page.Select(e => e.ActorDiscordId).Distinct().ToList();
        var actors = await usersRepository.FindAsync(u => actorIds.Contains(u.DiscordId), cancellationToken);
        var actorsById = actors.ToDictionary(u => u.DiscordId);

        var response = new GuildAuditLogPageResponse
        {
            HasMore = hasMore,
            Entries = [.. page.Select(entry => ToResponse(entry, actorsById, discordBotService, cancellationToken))],
        };

        return Result<GuildAuditLogPageResponse>.Ok(response);
    }

    /// <summary>
    /// Translates the query's <see cref="GetGuildAuditLogQuery.ActionType"/>/<see cref="GetGuildAuditLogQuery.Category"/>
    /// filters into the set of action types to pass to the repository. <see cref="GetGuildAuditLogQuery.ActionType"/>
    /// takes precedence when both are set.
    /// </summary>
    private static IReadOnlyCollection<GuildAuditAction>? ResolveActionTypeFilter(GetGuildAuditLogQuery query)
    {
        if (query.ActionType.HasValue)
            return [query.ActionType.Value];

        if (query.Category.HasValue)
            return [.. CategoryByAction.Where(kv => kv.Value == query.Category.Value).Select(kv => kv.Key)];

        return null;
    }

    private static AuditLogEntryResponse ToResponse(GuildAuditLog entry, Dictionary<string, User> actorsById, IDiscordBotService discordBotService, CancellationToken cancellationToken)
    {
        actorsById.TryGetValue(entry.ActorDiscordId, out var actor);
        var member = GuildMemberIdentityResolver.TryGetMember(discordBotService, entry.GuildId, entry.ActorDiscordId, cancellationToken);

        return new AuditLogEntryResponse
        {
            Id = entry.Id,
            ActorDiscordId = entry.ActorDiscordId,
            ActorUsername = member?.Nickname ?? member?.GlobalName ?? member?.Username ?? actor?.Name,
            // Prefer the bot's live Gateway cache over the DB snapshot (only refreshed at
            // login/token-refresh) — a member found in cache always wins, even when they have no
            // avatar at all (null is a meaningful live value, not "unknown, fall back to the DB").
            ActorAvatarHash = member is not null ? member.AvatarHash : actor?.AvatarHash,
            ActorGuildAvatarUrl = member?.HasGuildAvatar == true ? member.GetGuildAvatarUrl()?.ToString() : null,
            ActionType = entry.ActionType,
            Category = CategoryByAction[entry.ActionType],
            Variables = entry.Details != null
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(entry.Details)
                : null,
            OccurredAt = entry.OccurredAt,
        };
    }
}
