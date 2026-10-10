using Microsoft.Extensions.Logging;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.Access;

/// <inheritdoc cref="IGuildJoinEligibilityService"/>
public class GuildJoinEligibilityService(
    IGuildBranchesRepository guildBranchesRepository,
    IDiscordBotService discordBotService,
    ILogger<GuildJoinEligibilityService> logger) : IGuildJoinEligibilityService
{
    /// <inheritdoc/>
    public async Task<Result<GuildBranch>> ResolveEligibleBranchAsync(
        string guildId,
        int characterBranchId,
        string requesterDiscordId,
        CancellationToken cancellationToken = default)
    {
        var branch = await guildBranchesRepository.GetByGuildAndBranchAsync(guildId, characterBranchId, cancellationToken);
        if (branch == null || !branch.IsActive)
            return Result<GuildBranch>.Fail(ResponseDetail.GuildBranchNotActive, "This guild does not run this character's WoW branch.");

        if (branch.RosterMode == null)
            return Result<GuildBranch>.Fail(ResponseDetail.GuildNotConfigured, "This branch's roster settings have not been configured yet.");

        if (branch.RosterMode == RosterMode.DiscordRoleOnly)
        {
            var accessError = CheckDiscordRoleAccess(branch.RosterRoleIds, guildId, requesterDiscordId, cancellationToken);
            if (accessError != null)
                return accessError;
        }

        return Result<GuildBranch>.Ok(branch);
    }

    private Result<GuildBranch>? CheckDiscordRoleAccess(List<string> rosterRoleIds, string guildId, string requesterDiscordId, CancellationToken cancellationToken)
    {
        if (rosterRoleIds.Count == 0)
            return Result<GuildBranch>.Fail(ResponseDetail.GuildNotConfigured, "Roster role set is not configured.");

        try
        {
            var guildUser = discordBotService.Guilds.GetUsers(guildId, cancellationToken)
                .FirstOrDefault(u => u.Id.ToString() == requesterDiscordId);

            if (guildUser == null)
                return Result<GuildBranch>.Fail(ResponseDetail.RosterAccessDenied, "You are not found in this Discord server.");

            var heldRoleIds = guildUser.RoleIds.Select(r => r.ToString()).ToHashSet();
            var hasAccess = rosterRoleIds.Any(heldRoleIds.Contains);

            if (!hasAccess)
                return Result<GuildBranch>.Fail(ResponseDetail.RosterAccessDenied, "You do not have any of the required Discord roles to join this roster.");
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex,
                "Join guild {GuildId} failed for discord user {RequesterDiscordId}: RaidOps bot is not present in this guild",
                guildId, requesterDiscordId);
            return Result<GuildBranch>.Fail(ResponseDetail.GuildBotNotPresent, "The RaidOps bot is not present in this guild.");
        }

        return null;
    }
}
