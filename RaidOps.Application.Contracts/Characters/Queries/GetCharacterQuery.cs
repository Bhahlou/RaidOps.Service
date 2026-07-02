using RaidOps.Application.Contracts.Characters.Responses;
using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Characters.Queries;

/// <summary>
/// Returns a single character's detail by branch/realm/name, regardless of owner. The requester
/// must either own the character or share a guild roster with it (see <see cref="CharacterDetailResponse"/>
/// for the resulting permission flags).
/// </summary>
public class GetCharacterQuery : IQueryRequest<CharacterDetailResponse>
{
    /// <summary>Kebab-case slug of the game branch (e.g. "classic-anniversary"), as used in the front-end route.</summary>
    public required string BranchSlug { get; set; }

    /// <summary>Realm slug (e.g. "kazzak").</summary>
    public required string RealmSlug { get; set; }

    /// <summary>Character name, matched case-insensitively.</summary>
    public required string CharacterName { get; set; }

    /// <summary>Discord snowflake ID of the requesting user.</summary>
    public required string RequesterDiscordId { get; set; }
}
