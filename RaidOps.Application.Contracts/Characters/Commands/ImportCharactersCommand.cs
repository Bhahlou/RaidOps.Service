using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Characters.Commands;

/// <summary>
/// Command to import one or more WoW characters from the user's BNet account into RaidOps.
/// Each entry identifies a character by its BNet ID within a specific branch.
/// </summary>
public class ImportCharactersCommand : ICommandRequest
{
    /// <summary>Discord ID of the user performing the import.</summary>
    public required string UserDiscordId { get; set; }

    /// <summary>ID of the branch the characters are being imported from.</summary>
    public required int BranchId { get; set; }

    /// <summary>The characters selected for import.</summary>
    public required IEnumerable<CharacterToImportDto> Characters { get; set; }
}

/// <summary>
/// Identifies a single character to import, using the data already fetched from the BNet API
/// and validated on the client during the character selection step.
/// </summary>
public class CharacterToImportDto
{
    /// <summary>Blizzard's internal character ID.</summary>
    public long BnetCharacterId { get; set; }

    /// <summary>Character name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Realm slug (e.g. "kazzak"). Used to resolve or create the <c>Realm</c> record.</summary>
    public string RealmSlug { get; set; } = string.Empty;

    /// <summary>Localised realm name (e.g. "Kazzak"). Stored when the realm is first cached.</summary>
    public string RealmName { get; set; } = string.Empty;

    /// <summary>Blizzard class ID — maps directly to the seeded <c>WowClasses</c> PK.</summary>
    public int ClassId { get; set; }

    /// <summary>Blizzard race ID — maps directly to the seeded <c>Races</c> PK.</summary>
    public int RaceId { get; set; }

    /// <summary>Faction type string from BNet API ("ALLIANCE" or "HORDE").</summary>
    public string Faction { get; set; } = string.Empty;

    /// <summary>Current character level.</summary>
    public int Level { get; set; }
}
