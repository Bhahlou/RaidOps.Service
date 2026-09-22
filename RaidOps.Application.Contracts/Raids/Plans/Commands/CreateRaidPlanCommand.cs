using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Raids.Plans.Commands;

/// <summary>Creates a new strategy board for a boss.</summary>
public class CreateRaidPlanCommand : ICommandRequest
{
    /// <summary>Discord snowflake ID of the guild. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>Discord snowflake ID of the requesting user. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>FK to the boss this board is for.</summary>
    public int RaidBossId { get; set; }

    /// <summary>Display name.</summary>
    public required string Name { get; set; }
}
