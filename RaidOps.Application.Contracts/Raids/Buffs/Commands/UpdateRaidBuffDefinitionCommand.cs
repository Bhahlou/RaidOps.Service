using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Raids.Buffs.Commands;

/// <summary>
/// Edits one existing raid buff definition by its surrogate ID — unlike <see cref="UpsertRaidBuffDefinitionsCommand"/>,
/// which matches on the spell, this can change the definition's spell. The definition stays on its own expansion.
/// </summary>
public class UpdateRaidBuffDefinitionCommand : ICommandRequest
{
    /// <summary>Surrogate ID of the definition to edit.</summary>
    public required int Id { get; set; }

    /// <summary>The new values, including the (possibly changed) spell.</summary>
    public required RaidBuffDefinitionInput Definition { get; set; }
}
