namespace RaidOps.Application.Contracts.Raids.Spells.Responses;

/// <summary>A single spell search match, name already resolved to the requester's locale.</summary>
public class SpellResponse
{
    /// <summary>Blizzard's spell ID.</summary>
    public int Id { get; set; }

    /// <summary>Display name in the requester's locale.</summary>
    public required string Name { get; set; }

    /// <summary>Icon URL, hosted by RaidOps.</summary>
    public required string IconUrl { get; set; }
}
