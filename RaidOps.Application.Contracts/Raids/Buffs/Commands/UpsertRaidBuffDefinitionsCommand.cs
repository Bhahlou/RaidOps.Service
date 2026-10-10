using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Raids.Buffs.Commands;

/// <summary>
/// Creates or overwrites raid buff definitions for one expansion, matching each incoming entry to an
/// existing row by spell. Backs both the admin screen's single-row save (a list of one) and the JSON
/// import, so both go through one validation path. All-or-nothing: if any entry is invalid nothing is written.
/// </summary>
public class UpsertRaidBuffDefinitionsCommand : ICommandRequest
{
    /// <summary>The expansion the definitions apply to.</summary>
    public required int ExpansionId { get; set; }

    /// <summary>The definitions to write.</summary>
    public required List<RaidBuffDefinitionInput> Definitions { get; set; }

    /// <summary>
    /// When true, definitions of the expansion whose spell is absent from <see cref="Definitions"/> are
    /// deleted, so the expansion ends up exactly matching the list — used by the import to align environments.
    /// </summary>
    public bool PruneMissing { get; set; }
}
