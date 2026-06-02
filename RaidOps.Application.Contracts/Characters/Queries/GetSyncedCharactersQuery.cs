using RaidOps.Application.Contracts.Characters.Responses;
using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Characters.Queries;

/// <summary>
/// Returns all characters synced from BNet for the requesting user,
/// regardless of their <c>IsActiveInRaidOps</c> status.
/// Used to populate the character selection dialog.
/// </summary>
public class GetSyncedCharactersQuery : IQueryRequest<IEnumerable<SyncedCharacterDto>>
{
    /// <summary>Discord ID of the user whose synced characters to retrieve.</summary>
    public required string UserDiscordId { get; set; }
}
