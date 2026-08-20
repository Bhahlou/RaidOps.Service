using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Events.Responses;

namespace RaidOps.Application.Contracts.Raids.Events.Queries;

/// <summary>
/// Returns the characters currently assigned to a raid event — backs the "who should players
/// whisper?" dropdown shown when an officer triggers grouping without having a character of their
/// own assigned (see <c>TriggerRaidGroupingCommand</c>). The requesting user must hold
/// <see cref="Domain.Enums.GuildAccessLevel.Officer"/> access on <see cref="GuildId"/>.
/// </summary>
public class GetRaidEventAssignedCharactersQuery : IQueryRequest<List<RaidEventAssignedCharacterResponse>>
{
    /// <summary>Discord snowflake ID of the guild this event belongs to.</summary>
    public required string GuildId { get; set; }

    /// <summary>Surrogate ID of the guild branch this event belongs to.</summary>
    public required int GuildBranchId { get; set; }

    /// <summary>ID of the raid event whose assigned characters to list.</summary>
    public required int EventId { get; set; }

    /// <summary>Discord snowflake ID of the requesting user.</summary>
    public required string RequesterDiscordId { get; set; }
}
