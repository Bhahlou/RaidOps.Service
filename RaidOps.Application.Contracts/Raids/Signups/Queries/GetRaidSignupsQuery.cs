using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Signups.Responses;

namespace RaidOps.Application.Contracts.Raids.Signups.Queries;

/// <summary>
/// Returns every roster member's current response to a raid event in <see cref="Domain.Enums.SignupMode.Signup"/>
/// mode — backs the roster-facing signup list on the raids board (Discord-embed-style breakdown)
/// and the officer roster/status list on the raid detail page. The requesting user only needs
/// <see cref="Domain.Enums.GuildAccessLevel.Roster"/> access on <see cref="GuildId"/>: this is the
/// same information every roster member can already see on the Discord signup-call embed, so
/// there's nothing officer-exclusive about it.
/// </summary>
public class GetRaidSignupsQuery : IQueryRequest<List<RaidSignupResponse>>
{
    /// <summary>Discord snowflake ID of the guild this event belongs to.</summary>
    public required string GuildId { get; set; }

    /// <summary>Surrogate ID of the guild branch this event belongs to.</summary>
    public required int GuildBranchId { get; set; }

    /// <summary>ID of the raid event whose responses to list.</summary>
    public required int EventId { get; set; }

    /// <summary>Discord snowflake ID of the requesting user.</summary>
    public required string RequesterDiscordId { get; set; }
}
