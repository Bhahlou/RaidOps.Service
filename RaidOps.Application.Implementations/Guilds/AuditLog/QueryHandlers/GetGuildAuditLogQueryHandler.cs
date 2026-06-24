using System.Text.Json;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.AuditLog.Queries;
using RaidOps.Application.Contracts.Guilds.AuditLog.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.AuditLog.QueryHandlers;

/// <summary>
/// Handles <see cref="GetGuildAuditLogQuery"/> by verifying admin rights then returning a page
/// of the guild's audit log, enriched with actor display info.
/// </summary>
public class GetGuildAuditLogQueryHandler(
    IGuildAccessService guildAccessService,
    IGuildAuditLogRepository auditLogRepository,
    IUsersRepository usersRepository) : IQueryHandlerAsync<GetGuildAuditLogQuery, GuildAuditLogPageResponse>
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
            Entries = [.. page.Select(entry => ToResponse(entry, actorsById))],
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

    private static AuditLogEntryResponse ToResponse(GuildAuditLog entry, Dictionary<string, User> actorsById)
    {
        actorsById.TryGetValue(entry.ActorDiscordId, out var actor);

        return new AuditLogEntryResponse
        {
            Id = entry.Id,
            ActorDiscordId = entry.ActorDiscordId,
            ActorUsername = actor?.Name,
            ActorAvatarHash = actor?.AvatarHash,
            ActionType = entry.ActionType,
            Category = CategoryByAction[entry.ActionType],
            Variables = entry.Details != null
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(entry.Details)
                : null,
            OccurredAt = entry.OccurredAt,
        };
    }
}
