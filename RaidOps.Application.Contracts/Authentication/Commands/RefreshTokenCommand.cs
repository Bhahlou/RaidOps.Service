using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Authentication.Commands;

/// <summary>
/// Command used to exchange a valid RaidOps refresh token for a new access/refresh token pair.
/// </summary>
public class RefreshTokenCommand : ICommandRequest
{
    /// <summary>
    /// Gets or sets the RaidOps JWT refresh token to validate and exchange.
    /// </summary>
    public required string RefreshToken { get; set; }
}
