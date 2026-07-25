using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Series.Queries;
using RaidOps.Application.Contracts.Raids.Series.Responses;
using RaidOps.Application.Contracts.Raids.Zones.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Series.QueryHandlers;

/// <summary>
/// Handles <see cref="GetRaidSeriesListQuery"/> by returning every recurring template of a guild,
/// active or not, with its default zones and branch display name.
/// </summary>
public class GetRaidSeriesListQueryHandler(
    IGuildAccessService guildAccessService,
    IRaidSeriesRepository raidSeriesRepository,
    IBranchRepository branchRepository) : IQueryHandlerAsync<GetRaidSeriesListQuery, List<RaidSeriesResponse>>
{
    /// <inheritdoc/>
    public async Task<Result<List<RaidSeriesResponse>>> HandleAsync(GetRaidSeriesListQuery query, CancellationToken cancellationToken)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(query.RequesterDiscordId, query.GuildId, cancellationToken);
        if (accessLevel < GuildAccessLevel.Roster)
            return Result<List<RaidSeriesResponse>>.Fail(ResponseDetail.Forbidden, "User is not on this guild's roster.");

        var seriesList = await raidSeriesRepository.GetByGuildIdAsync(query.GuildId, cancellationToken);
        var branches = await branchRepository.GetAllAsync(cancellationToken);
        var branchesById = branches.ToDictionary(b => b.Id);

        return Result<List<RaidSeriesResponse>>.Ok([.. seriesList.Select(s => MapSeries(s, branchesById))]);
    }

    private static RaidSeriesResponse MapSeries(RaidSeries series, Dictionary<int, Branch> branchesById)
    {
        branchesById.TryGetValue(series.BranchId, out var branch);

        return new RaidSeriesResponse
        {
            Id = series.Id,
            Name = series.Name,
            BranchId = series.BranchId,
            BranchName = branch?.Name ?? string.Empty,
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
