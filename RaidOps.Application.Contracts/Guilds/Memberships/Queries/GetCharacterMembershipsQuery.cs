using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Memberships.Responses;

namespace RaidOps.Application.Contracts.Guilds.Memberships.Queries;

/// <summary>
/// Query that returns all guild roster memberships for a given character.
/// The requesting user must own the character.
/// </summary>
public class GetCharacterMembershipsQuery : IQueryRequest<List<GuildMembershipResponse>>
{
    /// <summary>Internal ID of the character.</summary>
    public required int CharacterId { get; set; }

    /// <summary>Discord snowflake ID of the requesting user.</summary>
    public required string RequesterDiscordId { get; set; }
}
