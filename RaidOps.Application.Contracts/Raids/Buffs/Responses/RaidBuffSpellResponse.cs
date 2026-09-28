namespace RaidOps.Application.Contracts.Raids.Buffs.Responses;

/// <summary>What a definition's spell is called and looks like on the definition's expansion, in every locale so the front picks its own.</summary>
public class RaidBuffSpellResponse
{
    /// <summary>English name on the expansion.</summary>
    public required string NameEn { get; set; }

    /// <summary>French name on the expansion.</summary>
    public required string NameFr { get; set; }

    /// <summary>German name on the expansion.</summary>
    public required string NameDe { get; set; }

    /// <summary>Icon URL on the expansion; empty when the spell has no resolvable icon.</summary>
    public required string IconUrl { get; set; }
}
