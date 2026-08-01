using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Services;

/// <inheritdoc cref="IRaidGridAndZoneValidator"/>
public class RaidGridAndZoneValidator(
    IGuildAccessService guildAccessService,
    IRaidZoneRepository raidZoneRepository) : IRaidGridAndZoneValidator
{
    /// <inheritdoc/>
    public async Task<Result<List<int>>> ValidateAsync(
        string requesterDiscordId,
        string guildId,
        int guildBranchId,
        int groupCount,
        int slotsPerGroup,
        IEnumerable<int> raidZoneIds,
        CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(requesterDiscordId, guildId, guildBranchId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<List<int>>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild branch.");

        if (groupCount <= 0 || slotsPerGroup <= 0)
            return Result<List<int>>.Fail(ResponseDetail.InvalidRequest, "GroupCount and SlotsPerGroup must be positive.");

        var distinctZoneIds = raidZoneIds.Distinct().ToList();
        if (distinctZoneIds.Count == 0)
            return Result<List<int>>.Fail(ResponseDetail.InvalidRequest, "At least one raid zone must be targeted.");

        var zones = await raidZoneRepository.GetByIdsAsync(distinctZoneIds, cancellationToken);
        if (zones.Count != distinctZoneIds.Count)
            return Result<List<int>>.Fail(ResponseDetail.RaidZoneNotFound, "One or more raid zones do not exist.");

        return Result<List<int>>.Ok(distinctZoneIds);
    }
}
