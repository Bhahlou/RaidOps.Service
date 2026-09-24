using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Raids.Attributions.Commands;

/// <summary>Permanently deletes a row of the guild's raid-attribution template, including every per-event fill it has across every raid event.</summary>
public class DeleteGuildAttributionDefinitionCommand : ICommandRequest
{
    /// <summary>Discord snowflake ID of the guild. Set by the controller, not from the request body.</summary>
    public required string GuildId { get; set; }

    /// <summary>Surrogate ID of the guild branch the row belongs to. Set by the controller from the route.</summary>
    public required int GuildBranchId { get; set; }

    /// <summary>Discord snowflake ID of the requesting user. Set by the controller, not from the request body.</summary>
    public required string RequesterDiscordId { get; set; }

    /// <summary>Surrogate ID of the definition being deleted. Set by the controller from the route.</summary>
    public required int DefinitionId { get; set; }
}
