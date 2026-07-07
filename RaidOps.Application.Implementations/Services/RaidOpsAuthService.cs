using Microsoft.Extensions.Logging;
using RaidOps.Application.Contracts.Authentication.Commands;
using RaidOps.Application.Contracts.Authentication.Responses;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Services;
using System.IdentityModel.Tokens.Jwt;

namespace RaidOps.Application.Implementations.Services;

/// <summary>
/// Implements <see cref="IRaidOpsAuthService"/> by orchestrating Discord data sync
/// via <see cref="IDiscordSyncService"/> and issuing RaidOps JWT pairs via <see cref="IJwtService"/>.
/// </summary>
public class RaidOpsAuthService(
    IDiscordSyncService discordSyncService,
    IJwtService jwtService,
    ILogger<RaidOpsAuthService> logger) : IRaidOpsAuthService
{
    /// <summary>
    /// Handles the Discord OAuth2 sign-up flow: syncs the user's profile and guilds
    /// using the supplied Discord tokens, then issues a new RaidOps token pair.
    /// </summary>
    /// <param name="command">The sign-up command containing the Discord user ID and OAuth2 tokens.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// A successful <see cref="Result{T}"/> with an <see cref="AuthenticationResponse"/>,
    /// or a failed result with the exception message if sync fails.
    /// </returns>
    public async Task<Result<AuthenticationResponse>> HandleSignupAsync(SignupCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await discordSyncService.SyncUserAndGuildsAsync(
                command.DiscordId,
                command.DiscordAccessToken,
                command.DiscordRefreshToken,
                cancellationToken);

            logger.LogInformation(
                "Signup completed for discord user {DiscordId}: {GuildCount} guild(s) synced",
                user.DiscordId, user.UserGuilds.Count);

            return Result<AuthenticationResponse>.Ok(GenerateTokens(user.DiscordId, user.Name));
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Signup failed for discord user {DiscordId}: Discord sync threw an exception",
                command.DiscordId);
            return Result<AuthenticationResponse>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// Handles a token refresh request: validates the supplied RaidOps refresh token,
    /// re-syncs Discord data using the stored Discord refresh token, then issues new tokens.
    /// </summary>
    /// <param name="command">The refresh command containing the existing RaidOps refresh JWT.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// A successful <see cref="Result{T}"/> with a new <see cref="AuthenticationResponse"/>,
    /// or a failed result if the token is invalid, the claims are missing, or the sync fails.
    /// </returns>
    public async Task<Result<AuthenticationResponse>> HandleRefreshTokenAsync(RefreshTokenCommand command, CancellationToken cancellationToken = default)
    {
        var principal = jwtService.ValidateRefreshToken(command.RefreshToken);
        if (principal == null)
        {
            logger.LogWarning("Token refresh failed: invalid or expired refresh token");
            return Result<AuthenticationResponse>.Fail(ResponseDetail.InvalidRefreshToken);
        }

        var discordId = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
        {
            logger.LogWarning("Token refresh failed: refresh token is missing the subject claim");
            return Result<AuthenticationResponse>.Fail(ResponseDetail.InvalidTokenClaims);
        }

        try
        {
            var user = await discordSyncService.SyncUserAndGuildsAsync(discordId, cancellationToken);

            logger.LogInformation(
                "Token refreshed for discord user {DiscordId}: {GuildCount} guild(s) re-synced",
                user.DiscordId, user.UserGuilds.Count);

            return Result<AuthenticationResponse>.Ok(GenerateTokens(user.DiscordId, user.Name));
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Token refresh failed for discord user {DiscordId}: Discord sync threw an exception",
                discordId);
            return Result<AuthenticationResponse>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// Generates a new access/refresh token pair for the given user and packages them
    /// into an <see cref="AuthenticationResponse"/>.
    /// </summary>
    /// <param name="discordId">The Discord snowflake ID of the user.</param>
    /// <param name="username">The user's Discord display name.</param>
    /// <returns>An <see cref="AuthenticationResponse"/> containing both tokens and their expiry times.</returns>
    private AuthenticationResponse GenerateTokens(string discordId, string username)
    {
        var (accessToken, accessExpiry) = jwtService.GenerateAccessToken(discordId, username);
        var (refreshToken, refreshExpiry) = jwtService.GenerateRefreshToken(discordId);

        return new AuthenticationResponse
        {
            AccessToken = accessToken,
            AccessTokenExpiration = accessExpiry,
            RefreshToken = refreshToken,
            RefreshTokenExpiration = refreshExpiry
        };
    }
}
