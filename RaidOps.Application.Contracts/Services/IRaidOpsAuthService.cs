using RaidOps.Application.Contracts.Authentication.Commands;
using RaidOps.Application.Contracts.Authentication.Responses;
using RaidOps.Application.Contracts.Common;

namespace RaidOps.Application.Contracts.Services;

/// <summary>
/// Orchestrates the RaidOps authentication flows: initial Discord OAuth2 sign-up
/// and subsequent silent token refresh.
/// </summary>
public interface IRaidOpsAuthService
{
    /// <summary>
    /// Handles the Discord OAuth2 sign-up flow: syncs user data and guilds from Discord,
    /// then issues a fresh pair of RaidOps JWT tokens.
    /// </summary>
    /// <param name="command">The sign-up command carrying the Discord user ID and OAuth2 tokens.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// A <see cref="Result{T}"/> containing an <see cref="AuthenticationResponse"/> with
    /// the new access and refresh tokens on success, or an error message on failure.
    /// </returns>
    Task<Result<AuthenticationResponse>> HandleSignupAsync(SignupCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles a token refresh request: validates the existing RaidOps refresh token,
    /// re-syncs Discord data using the stored Discord refresh token, then issues new tokens.
    /// </summary>
    /// <param name="command">The refresh command carrying the existing RaidOps refresh JWT.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// A <see cref="Result{T}"/> containing a new <see cref="AuthenticationResponse"/> on success,
    /// or an error message if the token is invalid or the user cannot be found.
    /// </returns>
    Task<Result<AuthenticationResponse>> HandleRefreshTokenAsync(RefreshTokenCommand command, CancellationToken cancellationToken = default);
}
