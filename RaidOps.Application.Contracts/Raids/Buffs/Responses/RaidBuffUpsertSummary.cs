namespace RaidOps.Application.Contracts.Raids.Buffs.Responses;

/// <summary>What an upsert of raid buff definitions changed — returned as the body of the command response.</summary>
public class RaidBuffUpsertSummary
{
    /// <summary>Definitions inserted because no row existed yet for their (expansion, spell).</summary>
    public int Created { get; set; }

    /// <summary>Existing definitions overwritten with the incoming values.</summary>
    public int Updated { get; set; }

    /// <summary>Definitions removed because they were absent from the incoming list and pruning was requested.</summary>
    public int Deleted { get; set; }
}
