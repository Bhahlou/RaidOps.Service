using RaidOps.Application.Contracts.Authentication.Queries;
using RaidOps.Application.Contracts.Authentication.Responses;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Authentication.QueryHandlers;

/// <summary>
/// Handles <see cref="GetMeQuery"/> by looking up the authenticated user in the
/// database and projecting the result into a <see cref="UserResponse"/>.
/// </summary>
public class GetMeQueryHandler(IUsersRepository usersRepository) : IQueryHandlerAsync<GetMeQuery, UserResponse>
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

        return Result<UserResponse>.Ok(new UserResponse
        {
            DiscordId = user.DiscordId,
            Name = user.Name,
            AvatarHash = user.AvatarHash,
            Guilds = user.UserGuilds
                .Where(ug => ug.IsAdmin || ug.Guild.IsRegistered)
                .Select(ug => new UserGuildResponse
                {
                    Id = ug.Guild.Id,
                    Name = ug.Guild.Name,
                    IconHash = ug.Guild.IconHash,
                    IsRegistered = ug.Guild.IsRegistered,
                    IsAdmin = ug.IsAdmin
                }).ToList()
        });
    }
}
