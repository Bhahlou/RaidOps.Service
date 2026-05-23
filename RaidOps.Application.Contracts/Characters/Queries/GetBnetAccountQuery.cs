using RaidOps.Application.Contracts.Characters.Responses;
using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Characters.Queries;

/// <summary>
/// Query that retrieves the Battle.net account linked to a given user.
/// Returns a failed result with error <c>"NOT_FOUND"</c> if no account has been linked yet.
/// </summary>
public class GetBnetAccountQuery : IQueryRequest<BnetAccountResponse>
{
    /// <summary>Gets or sets the Discord snowflake ID of the requesting user.</summary>
    public required string UserDiscordId { get; set; }
}
