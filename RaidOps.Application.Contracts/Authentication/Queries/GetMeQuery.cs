using RaidOps.Application.Contracts.Authentication.Responses;
using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Authentication.Queries;

/// <summary>
/// Query that retrieves the profile of the currently authenticated user.
/// </summary>
public class GetMeQuery : IQueryRequest<UserResponse>
{
    /// <summary>
    /// Gets or sets the Discord snowflake ID of the authenticated user.
    /// </summary>
    public required string DiscordId { get; set; }
}
