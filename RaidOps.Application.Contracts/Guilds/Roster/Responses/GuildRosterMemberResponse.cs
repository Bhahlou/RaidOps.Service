using RaidOps.Application.Contracts.Characters.Responses;
using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Guilds.Roster.Responses;

/// <summary>
/// A single character entry on a guild's roster. Returned by <c>GetGuildRosterQuery</c>.
/// </summary>
public class GuildRosterMemberResponse
{
    /// <summary>Internal character ID.</summary>
    public required int CharacterId { get; set; }

    /// <summary>Character name.</summary>
    public required string CharacterName { get; set; }

    /// <summary>FK to the character's class.</summary>
    public required int ClassId { get; set; }

    /// <summary>Display name of the character's class.</summary>
    public required string ClassName { get; set; }

    /// <summary>Hex color of the character's class, prefixed with '#'.</summary>
    public required string ClassColor { get; set; }

    /// <summary>Character level.</summary>
    public required int Level { get; set; }

    /// <summary>Display name of the game branch (e.g. "Classic Anniversary"). Used to build the character detail link.</summary>
    public required string BranchName { get; set; }

    /// <summary>Realm slug (e.g. "kazzak"). Used to build the character detail link.</summary>
    public required string RealmSlug { get; set; }

    /// <summary>Avatar image URL, or <c>null</c> if not yet synced.</summary>
    public string? AvatarUrl { get; set; }

    /// <summary>Discord snowflake ID of the player who owns this character.</summary>
    public required string PlayerDiscordId { get; set; }

    /// <summary>Discord display name of the player, or <c>null</c> if it could not be resolved.</summary>
    public string? PlayerName { get; set; }

    /// <summary>Discord avatar hash of the player, or <c>null</c> if it could not be resolved.</summary>
    public string? PlayerAvatarHash { get; set; }

    /// <summary>
    /// The player's server-specific avatar URL (Discord's per-guild avatar override), or
    /// <c>null</c> if they have none set for this guild. Falls back to <see cref="PlayerAvatarHash"/>
    /// when absent.
    /// </summary>
    public string? PlayerGuildAvatarUrl { get; set; }

    /// <summary>User-curated raid-viable specs, main spec first. Empty if none have been curated yet.</summary>
    public required List<CharacterRaidSpecDto> RaidSpecs { get; set; }

    /// <summary>Raid-composition rank of this character on the roster.</summary>
    public required CharacterRank CharacterRank { get; set; }

    /// <summary>UTC timestamp of when the character joined the roster.</summary>
    public required DateTime JoinedAt { get; set; }

    /// <summary>
    /// Whether the requester is allowed to exclude this character from the roster: they must be an
    /// Officer, and (unless this is the requester's own character) outrank the character's owner in
    /// the guild's Discord role hierarchy.
    /// </summary>
    public required bool CanExclude { get; set; }
}
