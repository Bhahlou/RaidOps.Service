using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Characters.Commands;

/// <summary>
/// Unlinks a Battle.net account from the requesting user and permanently deletes every character
/// that was sourced from it (right to erasure), along with their guild memberships.
/// </summary>
public class UnlinkBnetAccountCommand : ICommandRequest
{
    /// <summary>Discord ID of the requesting user.</summary>
    public required string UserDiscordId { get; set; }

    /// <summary>Blizzard account ID (<see cref="Responses.BnetAccountResponse.BnetId"/>) of the account to unlink.</summary>
    public required string BnetId { get; set; }
}
