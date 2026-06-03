using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Characters.Commands;

/// <summary>
/// Marks the given characters as active in RaidOps (<c>IsActiveInRaidOps = true</c>).
/// Characters must already be synced in the database and belong to the requesting user.
/// </summary>
public class ActivateCharactersCommand : ICommandRequest
{
    /// <summary>Discord ID of the user activating their characters.</summary>
    public required string UserDiscordId { get; set; }

    /// <summary>RaidOps internal IDs of the characters to activate.</summary>
    public required IEnumerable<int> CharacterIds { get; set; }
}
