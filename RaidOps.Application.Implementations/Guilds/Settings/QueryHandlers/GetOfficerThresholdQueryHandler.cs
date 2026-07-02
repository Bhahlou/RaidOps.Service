using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Settings.Queries;
using RaidOps.Application.Contracts.Guilds.Settings.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.Settings.QueryHandlers;

/// <summary>
/// Handles <see cref="GetOfficerThresholdQuery"/> by returning the guild's current Officer role threshold.
/// </summary>
public class GetOfficerThresholdQueryHandler(
    IGuildsRepository guildsRepository,
    IGuildAccessService guildAccessService) : IQueryHandlerAsync<GetOfficerThresholdQuery, OfficerThresholdResponse>
{
    /// <inheritdoc/>
    public async Task<Result<OfficerThresholdResponse>> HandleAsync(GetOfficerThresholdQuery query, CancellationToken cancellationToken)
    {
        var guild = await guildsRepository.GetByIdAsync(query.GuildId, cancellationToken);
        if (guild == null || !guild.IsRegistered)
            return Result<OfficerThresholdResponse>.Fail(ResponseDetail.GuildNotFound, "Guild not found or not registered.");

        var accessLevel = await guildAccessService.GetAccessLevelAsync(query.RequesterDiscordId, query.GuildId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<OfficerThresholdResponse>.Fail(ResponseDetail.Forbidden, "User is not an admin of this guild.");

        return Result<OfficerThresholdResponse>.Ok(new OfficerThresholdResponse { MinOfficerRoleId = guild.MinOfficerRoleId });
    }
}
