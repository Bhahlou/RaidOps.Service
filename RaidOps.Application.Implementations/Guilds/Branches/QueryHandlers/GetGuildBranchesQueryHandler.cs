using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Branches.Queries;
using RaidOps.Application.Contracts.Guilds.Branches.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.Branches.QueryHandlers;

/// <summary>
/// Handles <see cref="GetGuildBranchesQuery"/> by returning every WoW branch activated on a guild
/// (active and deactivated), for the guild's branches settings tab.
/// </summary>
public class GetGuildBranchesQueryHandler(
    IGuildsRepository guildsRepository,
    IGuildBranchesRepository guildBranchesRepository,
    IBranchRepository branchRepository,
    IGuildAccessService guildAccessService) : IQueryHandlerAsync<GetGuildBranchesQuery, List<GuildBranchResponse>>
{
    /// <inheritdoc/>
    public async Task<Result<List<GuildBranchResponse>>> HandleAsync(GetGuildBranchesQuery query, CancellationToken cancellationToken)
    {
        var guild = await guildsRepository.GetByIdAsync(query.GuildId, cancellationToken);
        if (guild == null || !guild.IsRegistered)
            return Result<List<GuildBranchResponse>>.Fail(ResponseDetail.GuildNotFound, "Guild not found or not registered.");

        var accessLevel = await guildAccessService.GetAccessLevelAsync(query.RequesterDiscordId, query.GuildId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<List<GuildBranchResponse>>.Fail(ResponseDetail.Forbidden, "User is not an admin of this guild.");

        var branches = await guildBranchesRepository.GetAllForGuildAsync(query.GuildId, cancellationToken);
        var wowBranches = (await branchRepository.GetAllAsync(cancellationToken)).ToDictionary(b => b.Id);

        var response = branches.Select(b => new GuildBranchResponse
        {
            Id = b.Id,
            BranchId = b.BranchId,
            BranchName = wowBranches.TryGetValue(b.BranchId, out var wowBranch) ? wowBranch.Name : "Unknown",
            IsActive = b.IsActive,
            RosterMode = b.RosterMode,
            RosterRoleIds = b.RosterRoleIds,
            OfficerRoleIds = b.OfficerRoleIds,
            Region = b.Region,
        }).ToList();

        return Result<List<GuildBranchResponse>>.Ok(response);
    }
}
