using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Characters.Commands;

/// <summary>
/// Sets <c>IsActiveInRaidOps = false</c> for the given character.
/// The character must belong to the requesting user.
/// Does not delete any related data.
/// </summary>
public class DeactivateCharacterCommand : ICommandRequest
{
    /// <summary>Discord ID of the requesting user.</summary>
    public required string UserDiscordId { get; set; }

    /// <summary>RaidOps internal ID of the character to deactivate.</summary>
    public required int CharacterId { get; set; }
}
