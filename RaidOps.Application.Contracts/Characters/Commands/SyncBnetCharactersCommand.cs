using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Characters.Commands;

/// <summary>
/// Fetches all WoW characters from the user's BNet account for the given branch
/// and upserts them in the database. Characters are synced with <c>IsActiveInRaidOps = false</c>
/// unless they were already activated.
/// </summary>
public class SyncBnetCharactersCommand : ICommandRequest
{
    /// <summary>Discord ID of the user triggering the sync.</summary>
    public required string UserDiscordId { get; set; }

    /// <summary>ID of the branch to sync characters from.</summary>
    public required int BranchId { get; set; }
}
