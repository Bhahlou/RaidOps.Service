using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Characters.Commands;

/// <summary>
/// Sets the user-curated specs a character is viable to raid with, replacing any previous set.
/// Unlike Battle.net-sourced specs, this is never touched by sync — only by this command,
/// usable both right after activation and as a later edit.
/// </summary>
public class SetCharacterRaidSpecsCommand : ICommandRequest
{
    /// <summary>Discord ID of the user requesting the change. Must own the character.</summary>
    public required string UserDiscordId { get; set; }

    /// <summary>RaidOps internal ID of the character.</summary>
    public required int CharacterId { get; set; }

    /// <summary>Blizzard spec ID of the character's main raid spec. Must be included in <see cref="ViableSpecIds"/>.</summary>
    public required int MainSpecId { get; set; }

    /// <summary>Blizzard spec IDs the character is viable to raid with, constrained to the character's class.</summary>
    public required IEnumerable<int> ViableSpecIds { get; set; }
}
