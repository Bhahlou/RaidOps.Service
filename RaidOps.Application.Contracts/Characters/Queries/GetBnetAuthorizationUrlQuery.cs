using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Characters.Queries;

/// <summary>
/// Returns the Battle.net OAuth2 authorization URL to which the user should be redirected.
/// Generating the URL requires signing a CSRF state token, which is a business concern
/// handled here rather than in the controller.
/// </summary>
public class GetBnetAuthorizationUrlQuery : IQueryRequest<string>
{
    /// <summary>Discord ID of the user initiating the BNet link.</summary>
    public required string DiscordId { get; set; }

    /// <summary>BNet region code selected by the user ("us", "eu", "kr", "tw").</summary>
    public required string Region { get; set; }

    /// <summary>The OAuth2 callback URL registered in the Battle.net developer portal.</summary>
    public required string CallbackUrl { get; set; }
}
