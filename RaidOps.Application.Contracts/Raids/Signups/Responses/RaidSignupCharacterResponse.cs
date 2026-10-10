namespace RaidOps.Application.Contracts.Raids.Signups.Responses;

/// <summary>One of the requester's own characters on a guild branch's roster — see <see cref="Queries.GetMyRosterCharactersQuery"/>.</summary>
public class RaidSignupCharacterResponse
{
    /// <summary>Internal character ID.</summary>
    public required int CharacterId { get; set; }

    /// <summary>Character name.</summary>
    public required string CharacterName { get; set; }

    /// <summary>Blizzard class ID — used to show the class icon in the character-select dropdown.</summary>
    public required int ClassId { get; set; }

    /// <summary>Display name of the character's WoW branch (e.g. "Classic Anniversary") — used, slugified, to deep-link to this character's RaidOps profile.</summary>
    public required string BranchName { get; set; }

    /// <summary>URL-safe realm slug — used, alongside <see cref="BranchName"/>, to deep-link to this character's RaidOps profile.</summary>
    public required string RealmSlug { get; set; }

    /// <summary>This character's declared raid-viable specs, main spec first.</summary>
    public IReadOnlyList<RaidSignupSpecResponse> RaidSpecs { get; set; } = [];
}
