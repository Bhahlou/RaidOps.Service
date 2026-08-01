using RaidOps.Application.Contracts.Characters.Responses;
using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Raids.Roster.Responses;

/// <summary>A roster character not currently assigned to any raid event in the requested range. Returned by <c>GetUnassignedGuildMembersQuery</c>.</summary>
public class UnassignedMemberResponse
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

    /// <summary>FK to the character's game branch.</summary>
    public required int BranchId { get; set; }

    /// <summary>Display name of the character's game branch. Used for client-side pool filtering by raid branch.</summary>
    public required string BranchName { get; set; }

    /// <summary>Avatar image URL, or <c>null</c> if not yet synced.</summary>
    public string? AvatarUrl { get; set; }

    /// <summary>Discord snowflake ID of the player who owns this character.</summary>
    public required string PlayerDiscordId { get; set; }

    /// <summary>Discord display name of the player, or <c>null</c> if it could not be resolved.</summary>
    public string? PlayerName { get; set; }

    /// <summary>User-curated raid-viable specs, main spec first. Empty if none have been curated yet.</summary>
    public required List<CharacterRaidSpecDto> RaidSpecs { get; set; }

    /// <summary>Raid-composition rank of this character on the roster.</summary>
    public required CharacterRank CharacterRank { get; set; }
}
