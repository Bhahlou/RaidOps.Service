namespace RaidOps.Application.Contracts.Raids.Buffs.Responses;

/// <summary>
/// A stored raid buff/debuff definition. Extends <see cref="RaidBuffDefinitionInput"/> so the admin
/// export can serialize the very same fields the import accepts.
/// </summary>
public class RaidBuffDefinitionResponse : RaidBuffDefinitionInput
{
    /// <summary>Surrogate ID of the definition — what the admin screen deletes by.</summary>
    public int Id { get; set; }

    /// <summary>The expansion the definition applies to.</summary>
    public int ExpansionId { get; set; }

    /// <summary>The spell's name/icon on <see cref="ExpansionId"/>, or <c>null</c> if the spell has no availability row there.</summary>
    public RaidBuffSpellResponse? Spell { get; set; }
}
