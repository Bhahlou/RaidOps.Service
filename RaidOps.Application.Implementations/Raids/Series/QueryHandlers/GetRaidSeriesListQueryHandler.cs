using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Series.Queries;
using RaidOps.Application.Contracts.Raids.Series.Responses;
using RaidOps.Application.Contracts.Raids.Zones.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Series.QueryHandlers;

/// <summary>
/// Handles <see cref="GetRaidSeriesListQuery"/> by returning every recurring template of a guild
/// branch, active or not, with its default zones and WoW-branch display name.
/// </summary>
public class GetRaidSeriesListQueryHandler(
    IGuildAccessService guildAccessService,
    IRaidSeriesRepository raidSeriesRepository) : IQueryHandlerAsync<GetRaidSeriesListQuery, List<RaidSeriesResponse>>
{
    /// <inheritdoc/>
    public async Task<Result<List<RaidSeriesResponse>>> HandleAsync(GetRaidSeriesListQuery query, CancellationToken cancellationToken)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(query.RequesterDiscordId, query.GuildId, query.GuildBranchId, cancellationToken);
        if (accessLevel < GuildAccessLevel.Roster)
            return Result<List<RaidSeriesResponse>>.Fail(ResponseDetail.Forbidden, "User is not on this guild branch's roster.");

        var seriesList = await raidSeriesRepository.GetByGuildBranchIdAsync(query.GuildBranchId, cancellationToken);

        return Result<List<RaidSeriesResponse>>.Ok([.. seriesList.Select(MapSeries)]);
    }

    private static RaidSeriesResponse MapSeries(RaidSeries series)
    {
        return new RaidSeriesResponse
        {
            Id = series.Id,
            Name = series.Name,
            BranchId = series.GuildBranch.BranchId,
            BranchName = series.GuildBranch.Branch.Name,
            RecurrenceDayOfWeek = series.RecurrenceDayOfWeek,
            RecurrenceStartTimeLocal = series.RecurrenceStartTimeLocal,
            RecurrenceIntervalWeeks = series.RecurrenceIntervalWeeks,
            GroupCount = series.GroupCount,
            SlotsPerGroup = series.SlotsPerGroup,
            SignupMode = series.SignupMode,
            IsActive = series.IsActive,
            RaidZones = [.. series.DefaultZones.Select(z => new RaidZoneRefResponse
            {
                Id = z.RaidZoneId,
                Name = z.RaidZone.Name,
                ShortCode = z.RaidZone.ShortCode,
            })],
        };
    }
}
