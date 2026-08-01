using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Lockout.Queries;
using RaidOps.Application.Contracts.Raids.Lockout.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Helpers;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Lockout.QueryHandlers;

/// <summary>
/// Handles <see cref="GetGuildBranchLockoutWeekQuery"/> by resolving the guild branch's regional
/// weekly reset schedule and computing which window currently covers "now", converted to the
/// guild's local calendar dates for display. Returns a fully-<c>null</c> response when the branch
/// has no <see cref="Domain.Models.Discord.GuildBranch.Region"/> configured yet — there's nothing to
/// resolve without it, and the caller is expected to fall back to its own default range.
/// </summary>
public class GetGuildBranchLockoutWeekQueryHandler(
    IGuildAccessService guildAccessService,
    IGuildsRepository guildsRepository,
    IGuildBranchesRepository guildBranchesRepository,
    IWeeklyLockoutScheduleRepository weeklyLockoutScheduleRepository,
    IRaidLockoutService raidLockoutService) : IQueryHandlerAsync<GetGuildBranchLockoutWeekQuery, GuildBranchLockoutWeekResponse>
{
    /// <inheritdoc/>
    public async Task<Result<GuildBranchLockoutWeekResponse>> HandleAsync(GetGuildBranchLockoutWeekQuery query, CancellationToken cancellationToken)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(query.RequesterDiscordId, query.GuildId, query.GuildBranchId, cancellationToken);
        if (accessLevel < GuildAccessLevel.Roster)
            return Result<GuildBranchLockoutWeekResponse>.Fail(ResponseDetail.Forbidden, "User is not on this guild branch's roster.");

        var guildBranch = await guildBranchesRepository.GetByIdAsync(query.GuildBranchId, cancellationToken);
        if (guildBranch == null || guildBranch.GuildId != query.GuildId)
            return Result<GuildBranchLockoutWeekResponse>.Fail(ResponseDetail.GuildBranchNotFound, "This guild branch does not exist.");

        if (string.IsNullOrWhiteSpace(guildBranch.Region))
            return Result<GuildBranchLockoutWeekResponse>.Ok(new GuildBranchLockoutWeekResponse());

        var schedule = await weeklyLockoutScheduleRepository.GetByRegionAsync(guildBranch.Region, cancellationToken);
        if (schedule == null)
            return Result<GuildBranchLockoutWeekResponse>.Ok(new GuildBranchLockoutWeekResponse());

        var windowStartUtc = raidLockoutService.GetLockoutWindowStart(schedule.AnchorUtc, schedule.CadenceDays, [], DateTime.UtcNow);
        var windowEndUtc = windowStartUtc.AddDays(schedule.CadenceDays).AddTicks(-1);

        var guild = await guildsRepository.GetByIdAsync(query.GuildId, cancellationToken);

        return Result<GuildBranchLockoutWeekResponse>.Ok(new GuildBranchLockoutWeekResponse
        {
            WeekStartLocal = GuildTimeHelper.ToGuildLocalDate(windowStartUtc, guild?.Timezone),
            WeekEndLocal = GuildTimeHelper.ToGuildLocalDate(windowEndUtc, guild?.Timezone),
        });
    }
}
