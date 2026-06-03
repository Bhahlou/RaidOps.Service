using RaidOps.Application.Contracts.Characters.Responses;
using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Characters.Queries;

/// <summary>Returns all characters imported by the requesting user.</summary>
public class GetCharactersQuery : IQueryRequest<IEnumerable<CharacterDto>>
{
    /// <summary>Discord ID of the user whose characters to retrieve.</summary>
    public required string UserDiscordId { get; set; }
}
