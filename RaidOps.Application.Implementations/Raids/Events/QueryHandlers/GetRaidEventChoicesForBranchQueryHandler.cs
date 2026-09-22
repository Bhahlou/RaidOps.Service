using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Events.Queries;
using RaidOps.Application.Contracts.Raids.Events.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Helpers;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Events.QueryHandlers;

/// <inheritdoc cref="GetRaidEventChoicesForBranchQuery"/>
public class GetRaidEventChoicesForBranchQueryHandler(
    IGuildAccessService guildAccessService,
    IRaidEventRepository raidEventRepository,
    IGuildsRepository guildsRepository,
    IGuildBranchesRepository guildBranchesRepository,
    IWeeklyLockoutScheduleRepository weeklyLockoutScheduleRepository,
    IRaidLockoutService raidLockoutService) : IQueryHandlerAsync<GetRaidEventChoicesForBranchQuery, List<RaidEventChoiceResponse>>
{
    // Fallback only, when the branch has no region/schedule to compute a real lockout window from —
    // wide enough to comfortably cover a multi-night extension planned a couple of weeks out either
    // way, without ever growing into "every raid this guild has ever run."
    private const int FallbackWindowDays = 60;

    /// <inheritdoc/>
    public async Task<Result<List<RaidEventChoiceResponse>>> HandleAsync(GetRaidEventChoicesForBranchQuery query, CancellationToken cancellationToken)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(query.RequesterDiscordId, query.GuildId, query.GuildBranchId, cancellationToken);
        if (accessLevel < GuildAccessLevel.Officer)
            return Result<List<RaidEventChoiceResponse>>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild branch.");

        var (rangeStartUtc, rangeEndUtc) = await ResolveRangeAsync(query.GuildBranchId, query.AroundStartsAtUtc, cancellationToken);

        var events = await raidEventRepository.GetForGuildBranchInRangeAsync(query.GuildBranchId, rangeStartUtc, rangeEndUtc, cancellationToken);
        var guild = await guildsRepository.GetByIdAsync(query.GuildId, cancellationToken);

        var response = events.Select(e => new RaidEventChoiceResponse
        {
            Id = e.Id,
            GuildBranchId = e.GuildBranchId,
            Name = e.Name,
            StartsAtLocal = GuildTimeHelper.ToGuildLocalDateTime(e.StartsAtUtc, guild?.Timezone),
            BranchName = e.GuildBranch.Branch.Name,
            ExtendsRaidEventId = e.ExtendsRaidEventId,
        }).ToList();

        return Result<List<RaidEventChoiceResponse>>.Ok(response);
    }

    /// <summary>
    /// Same regional-<see cref="Domain.Models.Raids.WeeklyLockoutSchedule"/> lookup as
    /// <c>GetGuildBranchLockoutWeekQueryHandler</c>, just centered on <paramref name="aroundStartsAtUtc"/>
    /// instead of "now" — doesn't account for a specific zone's own independent cadence
    /// (<see cref="Domain.Models.Raids.RaidZone.LockoutCadenceDays"/>) since the picker isn't scoped
    /// to one zone, only a reasonable branch-wide default.
    /// </summary>
    private async Task<(DateTime RangeStartUtc, DateTime RangeEndUtc)> ResolveRangeAsync(int guildBranchId, DateTime aroundStartsAtUtc, CancellationToken cancellationToken)
    {
        var guildBranch = await guildBranchesRepository.GetByIdAsync(guildBranchId, cancellationToken);
        if (string.IsNullOrWhiteSpace(guildBranch?.Region))
            return (aroundStartsAtUtc.AddDays(-FallbackWindowDays), aroundStartsAtUtc.AddDays(FallbackWindowDays));

        var schedule = await weeklyLockoutScheduleRepository.GetByRegionAsync(guildBranch.Region, cancellationToken);
        if (schedule == null)
            return (aroundStartsAtUtc.AddDays(-FallbackWindowDays), aroundStartsAtUtc.AddDays(FallbackWindowDays));

        var windowStartUtc = raidLockoutService.GetLockoutWindowStart(schedule.AnchorUtc, schedule.CadenceDays, [], aroundStartsAtUtc);
        var windowEndUtc = windowStartUtc.AddDays(schedule.CadenceDays).AddTicks(-1);
        return (windowStartUtc, windowEndUtc);
    }
}
