using Microsoft.Extensions.Logging;
using NetCord;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.Access;

/// <summary>
/// Default <see cref="IGuildAccessService"/> implementation. Single source of truth for the
/// Public/Roster/Officer hierarchy, replacing the admin and Discord-role checks that were
/// previously duplicated across guild-scoped handlers.
/// </summary>
public class GuildAccessService(
    IUserGuildsRepository userGuildsRepository,
    IGuildsRepository guildsRepository,
    IGuildBranchesRepository guildBranchesRepository,
    IDiscordBotService discordBotService,
    ILogger<GuildAccessService> logger) : IGuildAccessService
{
    /// <inheritdoc/>
    public async Task<GuildAccessLevel> GetAccessLevelAsync(string discordId, string guildId, CancellationToken cancellationToken = default)
    {
        var membership = await GetMembershipAsync(discordId, guildId, cancellationToken);
        if (membership == null)
            return GuildAccessLevel.None;

        if (membership.IsAdmin)
            return GuildAccessLevel.Officer;

        var guild = await guildsRepository.GetByIdAsync(guildId, cancellationToken);
        if (guild == null || !guild.IsRegistered)
            return GuildAccessLevel.None;

        var branches = await guildBranchesRepository.GetActiveForGuildAsync(guildId, cancellationToken);

        var highest = GuildAccessLevel.Public;
        foreach (var branch in branches)
        {
            var level = ComputeAccessLevel(membership, branch, cancellationToken);
            if (level > highest)
                highest = level;
        }

        return highest;
    }

    /// <inheritdoc/>
    public async Task<GuildAccessLevel> GetAccessLevelAsync(string discordId, string guildId, int guildBranchId, CancellationToken cancellationToken = default)
    {
        var membership = await GetMembershipAsync(discordId, guildId, cancellationToken);
        if (membership == null)
            return GuildAccessLevel.None;

        if (membership.IsAdmin)
            return GuildAccessLevel.Officer;

        var branch = await guildBranchesRepository.GetByIdAsync(guildBranchId, cancellationToken);
        if (branch == null || branch.GuildId != guildId || !branch.IsActive)
            return GuildAccessLevel.None;

        var guild = await guildsRepository.GetByIdAsync(guildId, cancellationToken);
        if (guild == null || !guild.IsRegistered)
            return GuildAccessLevel.None;

        return ComputeAccessLevel(membership, branch, cancellationToken);
    }

    /// <inheritdoc/>
    public GuildAccessLevel ComputeAccessLevel(UserGuild membership, GuildBranch branch, CancellationToken cancellationToken = default)
    {
        if (membership.IsAdmin)
            return GuildAccessLevel.Officer;

        if (DiscordRoleSetAccessHelper.HasAnyDiscordRole(discordBotService, branch.GuildId, branch.OfficerRoleIds, membership.UserDiscordId, cancellationToken))
            return GuildAccessLevel.Officer;

        var hasRosterAccess = branch.RosterMode switch
        {
            RosterMode.Open => true,
            RosterMode.DiscordRoleOnly => DiscordRoleSetAccessHelper.HasAnyDiscordRole(discordBotService, branch.GuildId, branch.RosterRoleIds, membership.UserDiscordId, cancellationToken),
            _ => false,
        };

        return hasRosterAccess ? GuildAccessLevel.Roster : GuildAccessLevel.Public;
    }

    /// <inheritdoc/>
    public async Task<bool> OutranksAsync(string guildId, int guildBranchId, string requesterDiscordId, string targetDiscordId, CancellationToken cancellationToken = default)
    {
        var requesterMembership = await GetMembershipAsync(requesterDiscordId, guildId, cancellationToken);
        var targetMembership = await GetMembershipAsync(targetDiscordId, guildId, cancellationToken);

        // The owner is never outranked, not even by another admin.
        if (targetMembership?.IsOwner == true)
            return false;

        if (requesterMembership?.IsAdmin == true)
            return true;
        if (targetMembership?.IsAdmin == true)
            return false;

        var branch = await guildBranchesRepository.GetByIdAsync(guildBranchId, cancellationToken);
        if (branch != null)
        {
            var requesterIsOfficer = DiscordRoleSetAccessHelper.HasAnyDiscordRole(discordBotService, guildId, branch.OfficerRoleIds, requesterDiscordId, cancellationToken);
            var targetIsOfficer = DiscordRoleSetAccessHelper.HasAnyDiscordRole(discordBotService, guildId, branch.OfficerRoleIds, targetDiscordId, cancellationToken);

            if (requesterIsOfficer != targetIsOfficer)
                return requesterIsOfficer;
        }

        var requesterPosition = GetHighestRolePosition(guildId, requesterDiscordId, cancellationToken);
        var targetPosition = GetHighestRolePosition(guildId, targetDiscordId, cancellationToken);

        if (requesterPosition == null)
            return false;
        if (targetPosition == null)
            return true;

        return requesterPosition.Value > targetPosition.Value;
    }

    private async Task<UserGuild?> GetMembershipAsync(string discordId, string guildId, CancellationToken cancellationToken)
    {
        var userGuilds = await userGuildsRepository.GetByUserDiscordIdAsync(discordId, cancellationToken);
        return userGuilds.FirstOrDefault(ug => ug.GuildId == guildId);
    }

    /// <summary>
    /// Returns the highest position among the Discord roles held by <paramref name="discordId"/> in
    /// <paramref name="guildId"/>, or <c>null</c> if the user holds no roles, isn't found in the
    /// guild, or the bot isn't present there. <c>null</c> deliberately ranks below every real role
    /// position, so a user with no roles is always outranked.
    /// </summary>
    private RolePosition? GetHighestRolePosition(string guildId, string discordId, CancellationToken cancellationToken)
    {
        try
        {
            var roles = discordBotService.Guilds.GetRoles(guildId, cancellationToken)
                .ToDictionary(r => r.Id.ToString());

            var guildUser = discordBotService.Guilds.GetUsers(guildId, cancellationToken)
                .FirstOrDefault(u => u.Id.ToString() == discordId);

            if (guildUser == null)
                return null;

            RolePosition? highestPosition = null;
            foreach (var roleId in guildUser.RoleIds)
            {
                if (roles.TryGetValue(roleId.ToString(), out var role) && (highestPosition == null || role.Position >= highestPosition.Value))
                    highestPosition = role.Position;
            }

            return highestPosition;
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex,
                "Discord role position lookup failed for discord user {DiscordId} in guild {GuildId}: RaidOps bot is not present in this guild",
                discordId, guildId);
            return null;
        }
    }
}
