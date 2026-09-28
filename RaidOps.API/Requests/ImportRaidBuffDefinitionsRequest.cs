using RaidOps.Application.Contracts.Raids.Buffs;

namespace RaidOps.API.Requests;

/// <summary>Request body for <c>POST /api/v1/admin/raid-buffs/{expansionId}/import</c> — the contents of an exported definitions file.</summary>
public class ImportRaidBuffDefinitionsRequest
{
    /// <summary>The definitions to write to the expansion.</summary>
    public required List<RaidBuffDefinitionInput> Definitions { get; set; }

    /// <summary>When true, definitions of the expansion absent from <see cref="Definitions"/> are deleted, so the expansion ends up matching the file exactly.</summary>
    public bool PruneMissing { get; set; }
}
