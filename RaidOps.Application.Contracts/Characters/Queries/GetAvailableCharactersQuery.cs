using RaidOps.Application.Contracts.Characters.Responses;
using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Characters.Queries;

/// <summary>
/// Query that fetches the list of WoW characters available for import from the BNet API
/// for a given user and branch.
/// Returns a flat list of characters with an <c>AlreadyImported</c> flag.
/// </summary>
public class GetAvailableCharactersQuery : IQueryRequest<IEnumerable<AvailableCharacterDto>>
{
    /// <summary>Discord ID of the requesting user — used to check already-imported characters.</summary>
    public required string UserDiscordId { get; set; }

    /// <summary>ID of the branch to query characters for (determines the BNet profile namespace).</summary>
    public required int BranchId { get; set; }
}
