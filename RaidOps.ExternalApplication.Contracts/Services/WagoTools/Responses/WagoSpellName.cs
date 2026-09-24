namespace RaidOps.ExternalApplication.Contracts.Services.WagoTools.Responses;

/// <summary>One row of the <c>SpellName</c> DB2 CSV export — a spell ID and its localized name.</summary>
public class WagoSpellName
{
    /// <summary>Blizzard's spell ID.</summary>
    public required int Id { get; set; }

    /// <summary>The spell's name in the locale that was requested.</summary>
    public required string Name { get; set; }
}
