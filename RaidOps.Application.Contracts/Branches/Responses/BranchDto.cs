namespace RaidOps.Application.Contracts.Branches.Responses;

/// <summary>
/// Lightweight representation of a WoW branch returned by <c>GET /api/v1/branches</c>.
/// Used in the character import dialog to let the user pick the game branch to import from.
/// </summary>
public class BranchDto
{
    /// <summary>Seeded branch ID.</summary>
    public int Id { get; set; }

    /// <summary>Display name shown in the branch picker (e.g. "Classic Anniversary", "Retail").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// BNet API namespace prefix used to derive the profile namespace.
    /// Append "-{region}" at query time (e.g. "dynamic" + "-eu" → "dynamic-eu").
    /// </summary>
    public string BnetNamespacePrefix { get; set; } = string.Empty;

    /// <summary>Short code of the expansion currently active on this branch (e.g. "TWW", "TBC").</summary>
    public string CurrentExpansionShortCode { get; set; } = string.Empty;
}
