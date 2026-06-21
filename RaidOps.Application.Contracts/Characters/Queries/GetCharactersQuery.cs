using RaidOps.Application.Contracts.Characters.Responses;
using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Characters.Queries;

/// <summary>Returns all characters imported by the requesting user, alongside their linked Battle.net account.</summary>
public class GetCharactersQuery : IQueryRequest<GetCharactersResponse>
{
    /// <summary>Discord ID of the user whose characters to retrieve.</summary>
    public required string UserDiscordId { get; set; }
}
