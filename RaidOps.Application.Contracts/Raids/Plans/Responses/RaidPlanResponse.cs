namespace RaidOps.Application.Contracts.Raids.Plans.Responses;

/// <summary>A guild's visual strategy board for one boss.</summary>
public class RaidPlanResponse
{
    /// <summary>Surrogate ID of the board.</summary>
    public int Id { get; set; }

    /// <summary>Display name.</summary>
    public required string Name { get; set; }

    /// <summary>FK to the boss this board is for.</summary>
    public int RaidBossId { get; set; }
}
