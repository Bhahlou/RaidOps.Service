using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Characters.Commands;

/// <summary>
/// Encapsulates the entire BNet OAuth2 callback: state validation, token exchange,
/// account linking, and optional character sync.
/// </summary>
public class HandleBnetCallbackCommand : ICommandRequest
{
    /// <summary>Discord ID of the authenticated user (from the RaidOps JWT cookie).</summary>
    public required string DiscordId { get; set; }

    /// <summary>Authorization code returned by Battle.net.</summary>
    public required string Code { get; set; }

    /// <summary>CSRF state token generated during the initiation step.</summary>
    public required string State { get; set; }

    /// <summary>The callback URL used during code exchange (must match the initiation redirect URI).</summary>
    public required string CallbackUrl { get; set; }
}
