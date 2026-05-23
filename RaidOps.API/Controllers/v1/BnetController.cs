using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaidOps.Application.Contracts.Characters.Commands;
using RaidOps.Application.Contracts.Characters.Queries;
using RaidOps.Application.Contracts.Characters.Responses;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Services;
using RaidOps.ExternalApplication.Contracts.Services.BNet;
using System.IdentityModel.Tokens.Jwt;

namespace RaidOps.API.Controllers.v1;

/// <summary>
/// Exposes Battle.net account linking endpoints via the BNet OAuth2 flow.
/// All routes require a valid JWT Bearer token (the user must already be logged in via Discord).
/// </summary>
[ApiVersion("1.0")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class BnetController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    IJwtService jwtService,
    IBnetApiService bnetApiService,
    IConfiguration configuration) : ApiControllerBase(commandDispatcher, queryDispatcher)
{
    private static readonly HashSet<string> ValidRegions = ["us", "eu", "kr", "tw"];

    private readonly string _frontendUrl = configuration["FrontendUrl"] ?? "http://localhost:4200";

    /// <summary>
    /// Builds the BNet OAuth callback URL in a consistent, lowercase, predictable form
    /// that can be registered verbatim in the Battle.net developer portal.
    /// </summary>
    private string CallbackUrl =>
        $"{Request.Scheme}://{Request.Host}/api/v1.0/bnet/link/callback";

    /// <summary>
    /// Returns the Battle.net account linked to the authenticated user,
    /// or 404 if no account has been linked yet.
    /// </summary>
    /// <returns>
    /// 200 with <see cref="BnetAccountResponse"/>, or 404 if not linked.
    /// </returns>
    [HttpGet("account")]
    public async Task<IActionResult> GetAccount(CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null) return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetBnetAccountQuery, BnetAccountResponse>(new GetBnetAccountQuery
        {
            UserDiscordId = discordId
        }, cancellationToken);

        if (result.IsFailed)
            return result.Error == "NOT_FOUND" ? NotFound() : BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }

    /// <summary>
    /// Initiates the Battle.net OAuth2 flow for account linking.
    /// Validates the requested region, generates a signed CSRF state token,
    /// then redirects the user to the Battle.net authorization page.
    /// </summary>
    /// <param name="region">BNet region the user selected: "us", "eu", "kr", or "tw".</param>
    /// <returns>A 302 redirect to the Battle.net authorization URL.</returns>
    [HttpGet("link/initiate")]
    public IActionResult Initiate([FromQuery] string region)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null) return Unauthorized();

        if (!ValidRegions.Contains(region.ToLower()))
            return BadRequest("Invalid region. Must be one of: us, eu, kr, tw.");

        region = region.ToLower();

        var state = jwtService.GenerateBnetStateToken(discordId, region);
        var callbackUrl = CallbackUrl;
        var bnetUrl = bnetApiService.BuildAuthorizationUrl(region, callbackUrl, state);

        return Redirect(bnetUrl);
    }

    /// <summary>
    /// Handles the Battle.net OAuth2 callback after the user authorizes the application.
    /// Validates the CSRF state, exchanges the authorization code for tokens,
    /// fetches the user's BattleTag, persists the linked account, then redirects to the front-end.
    /// </summary>
    /// <param name="code">Authorization code returned by Battle.net.</param>
    /// <param name="state">CSRF state token generated during <see cref="Initiate"/>.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// A 302 redirect to <c>/characters?bnet_linked=true</c> on success,
    /// or <c>/characters?error=…</c> on failure.
    /// </returns>
    [HttpGet("link/callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Redirect($"{_frontendUrl}/characters?error=unauthorized");

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            return Redirect($"{_frontendUrl}/characters?error=invalid_request");

        var stateData = jwtService.ValidateBnetStateToken(state);
        if (stateData == null)
            return Redirect($"{_frontendUrl}/characters?error=invalid_state");

        if (stateData.Value.DiscordId != discordId)
            return Redirect($"{_frontendUrl}/characters?error=state_mismatch");

        var region = stateData.Value.Region;
        var callbackUrl = CallbackUrl;

        try
        {
            // Exchange code for tokens
            var tokenResponse = await bnetApiService.ExchangeCodeAsync(code, callbackUrl, region, cancellationToken);

            // Fetch BattleTag + account ID
            var userInfo = await bnetApiService.GetUserInfoAsync(tokenResponse.AccessToken, region, cancellationToken);

            var tokenExpiry = DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

            var result = await CommandDispatcher.DispatchAsync(new LinkBnetAccountCommand
            {
                UserDiscordId = discordId,
                BnetId = userInfo.Id.ToString(),
                BattleTag = userInfo.BattleTag,
                AccessToken = tokenResponse.AccessToken,
                RefreshToken = tokenResponse.RefreshToken,
                TokenExpiry = tokenExpiry,
                Region = region
            }, cancellationToken);

            if (result.IsFailed)
                return Redirect($"{_frontendUrl}/characters?error=link_failed");

            return Redirect($"{_frontendUrl}/characters?bnet_linked=true");
        }
        catch (HttpRequestException)
        {
            return Redirect($"{_frontendUrl}/characters?error=bnet_api_error");
        }
    }
}
