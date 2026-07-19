using RaidOps.Application.Contracts.Characters.Responses;
using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Characters.Queries;

/// <summary>
/// Query that retrieves all Battle.net accounts linked to a given user.
/// Returns an empty list if none have been linked yet.
/// </summary>
public class GetBnetAccountsQuery : IQueryRequest<List<BnetAccountResponse>>
{
    /// <summary>Gets or sets the Discord snowflake ID of the requesting user.</summary>
    public required string UserDiscordId { get; set; }
}
