using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Settings.Queries;
using RaidOps.Application.Contracts.Guilds.Settings.Responses;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.Settings.QueryHandlers;

/// <summary>
/// Handles <see cref="GetGuildSettingsQuery"/> by returning the stored settings of a registered guild.
/// </summary>
public class GetGuildSettingsQueryHandler(
    IGuildsRepository guildsRepository) : IQueryHandlerAsync<GetGuildSettingsQuery, GuildSettingsResponse>
{
    /// <inheritdoc/>
    public async Task<Result<GuildSettingsResponse>> HandleAsync(
        GetGuildSettingsQuery query,
        CancellationToken cancellationToken)
    {
        var guild = await guildsRepository.GetByIdAsync(query.GuildId, cancellationToken);

        if (guild == null || !guild.IsRegistered)
            return Result<GuildSettingsResponse>.Fail(ResponseDetail.GuildNotFound, "Guild not found or not registered.");

        return Result<GuildSettingsResponse>.Ok(new GuildSettingsResponse
        {
            Timezone = guild.Timezone,
            RosterMode = guild.RosterMode ?? RosterMode.Open,
            MinRosterRoleId = guild.MinRosterRoleId,
        });
    }
}
