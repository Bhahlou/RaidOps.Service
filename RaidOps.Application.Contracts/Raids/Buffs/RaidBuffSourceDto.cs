namespace RaidOps.Application.Contracts.Raids.Buffs;

/// <summary>One class/spec able to provide a raid buff or debuff — used both to write a definition and to read it back.</summary>
public class RaidBuffSourceDto
{
    /// <summary>Blizzard class ID of the providing class.</summary>
    public int ClassId { get; set; }

    /// <summary>Blizzard spec ID of the providing spec, or <c>null</c> when any spec of the class provides it.</summary>
    public int? SpecId { get; set; }
}
