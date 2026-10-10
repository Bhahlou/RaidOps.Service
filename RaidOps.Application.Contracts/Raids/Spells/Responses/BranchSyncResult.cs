namespace RaidOps.Application.Contracts.Raids.Spells.Responses;

/// <summary>What happened when the spell-sync job checked one branch against wago.tools.</summary>
public class BranchSyncResult
{
    /// <summary>The branch that was checked.</summary>
    public required int BranchId { get; set; }

    /// <summary>The branch's display name, e.g. "Forever".</summary>
    public required string BranchName { get; set; }

    /// <summary>The build version this branch was synced up to before this run, if any.</summary>
    public string? PreviousBuild { get; set; }

    /// <summary>The current build version on wago.tools for this branch's product.</summary>
    public required string LatestBuild { get; set; }

    /// <summary>True if the branch was already up to date and no data was pulled.</summary>
    public required bool Skipped { get; set; }

    /// <summary>Number of spells newly inserted.</summary>
    public int AddedCount { get; set; }

    /// <summary>Number of existing spells whose name changed.</summary>
    public int RenamedCount { get; set; }
}
