using RaidOps.Application.Contracts.Characters.Queries;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Services;
using RaidOps.ExternalApplication.Contracts.Services.BNet;

namespace RaidOps.Application.Implementations.Characters.QueryHandlers;

/// <summary>
/// Handles <see cref="GetBnetAuthorizationUrlQuery"/> by generating a signed CSRF state token
/// and building the Battle.net OAuth2 authorization URL.
/// </summary>
public class GetBnetAuthorizationUrlQueryHandler(
    IJwtService jwtService,
    IBnetApiService bnetApiService)
    : IQueryHandlerAsync<GetBnetAuthorizationUrlQuery, string>
{
    /// <summary>
    /// Returns the fully-formed BNet OAuth2 authorization URL including the signed state token.
    /// </summary>
    public Task<Result<string>> HandleAsync(
        GetBnetAuthorizationUrlQuery query,
        CancellationToken cancellationToken)
    {
        var state = jwtService.GenerateBnetStateToken(query.DiscordId, query.Region);
        var url = bnetApiService.BuildAuthorizationUrl(query.Region, query.CallbackUrl, state);
        return Task.FromResult(Result<string>.Ok(url));
    }
}
