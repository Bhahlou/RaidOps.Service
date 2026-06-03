using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Characters.Commands;

/// <summary>
/// Re-fetches the character's data from the Battle.net API
/// and returns the updated <see cref="RaidOps.Application.Contracts.Characters.Responses.CharacterDto"/>.
/// The character must be active and belong to the requesting user.
/// </summary>
public class ResyncCharacterCommand : ICommandRequest
{
    /// <summary>Discord ID of the requesting user.</summary>
    public required string UserDiscordId { get; set; }

    /// <summary>RaidOps internal ID of the character to resync.</summary>
    public required int CharacterId { get; set; }
}
