using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Signups.Responses;

namespace RaidOps.Application.Contracts.Raids.Signups.Queries;

/// <summary>
/// Returns the requesting member's own characters on this guild branch's roster — the pool a
/// Signup-mode Accept RSVP picks from, both on the web (character count decides whether a picker is
/// shown) and from Discord (populates the character-select modal). The requesting user must hold
/// at least <see cref="Domain.Enums.GuildAccessLevel.Roster"/> access on <see cref="GuildId"/>.
/// </summary>
public class GetMyRosterCharactersQuery : IQueryRequest<List<RaidSignupCharacterResponse>>
{
    /// <summary>Discord snowflake ID of the guild this branch belongs to.</summary>
    public required string GuildId { get; set; }

    /// <summary>Surrogate ID of the guild branch to list roster characters for.</summary>
    public required int GuildBranchId { get; set; }

    /// <summary>Discord snowflake ID of the requesting user.</summary>
    public required string RequesterDiscordId { get; set; }
}
