namespace RaidOps.API.Requests;

/// <summary>Request body for <c>POST /api/v1/characters/sync</c>.</summary>
public class SyncBnetCharactersRequest
{
    /// <summary>ID of the branch to sync characters from.</summary>
    public required int BranchId { get; set; }
}
