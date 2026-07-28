using RaidOps.Application.Contracts.Authentication.Queries;
using RaidOps.Application.Contracts.Authentication.Responses;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Authentication.QueryHandlers;

/// <summary>
/// Handles <see cref="GetMeQuery"/> by looking up the authenticated user in the
/// database and projecting the result into a <see cref="UserResponse"/>.
/// </summary>
public class GetMeQueryHandler(
    IUsersRepository usersRepository,
    IGuildAccessService guildAccessService,
    IUserNotificationService userNotificationService,
    IActiveRosterBranchResolver activeRosterBranchResolver) : IQueryHandlerAsync<GetMeQuery, UserResponse>
{
    /// <summary>
    /// Retrieves the user identified by <see cref="GetMeQuery.DiscordId"/> and maps them
    /// to a <see cref="UserResponse"/>.
    /// </summary>
    /// <param name="query">The query containing the Discord ID of the requesting user.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// A successful <see cref="Result{T}"/> with the user's profile, or a failed result
    /// if no user with the given Discord ID exists.
    /// </returns>
    public async Task<Result<UserResponse>> HandleAsync(GetMeQuery query, CancellationToken cancellationToken)
    {
        var user = await usersRepository.GetByDiscordIdWithGuildsAsync(query.DiscordId, cancellationToken);
        if (user == null)
            return Result<UserResponse>.Fail(ResponseDetail.UserNotFound);

        var eligibleGuilds = user.UserGuilds.Where(ug => ug.IsAdmin || ug.Guild.IsRegistered).ToList();

        var activeBranches = (await activeRosterBranchResolver.GetActiveBranchesAsync(query.DiscordId, cancellationToken))
            .Select(b => (b.GuildId, b.GuildBranchId))
            .ToHashSet();

        var guilds = eligibleGuilds.Select(ug =>
        {
            var branches = ug.Guild.Branches
                .Where(b => b.IsActive)
                .Select(b => new UserGuildBranchResponse
                {
                    Id = b.Id,
                    BranchId = b.BranchId,
                    BranchName = b.Branch.Name,
                    AccessLevel = guildAccessService.ComputeAccessLevel(ug, b, cancellationToken),
                    HasActiveCharacter = activeBranches.Contains((ug.Guild.Id, b.Id)),
                })
                .ToList();

            var maxBranchAccessLevel = branches.Count > 0 ? branches.Max(b => b.AccessLevel) : GuildAccessLevel.Public;
            var accessLevel = ug.IsAdmin ? GuildAccessLevel.Officer : maxBranchAccessLevel;

            return new UserGuildResponse
            {
                Id = ug.Guild.Id,
                Name = ug.Guild.Name,
                IconHash = ug.Guild.IconHash,
                IsRegistered = ug.Guild.IsRegistered,
                IsConfigured = ug.Guild.Timezone != null && ug.Guild.Language != null,
                IsAdmin = ug.IsAdmin,
                Branches = branches,
                AccessLevel = accessLevel,
            };
        }).ToList();

        return Result<UserResponse>.Ok(new UserResponse
        {
            DiscordId = user.DiscordId,
            Name = user.Name,
            AvatarHash = user.AvatarHash,
            Guilds = guilds,
            Notifications = await userNotificationService.GetActiveNotificationsAsync(query.DiscordId, eligibleGuilds, cancellationToken),
        });
    }
}
