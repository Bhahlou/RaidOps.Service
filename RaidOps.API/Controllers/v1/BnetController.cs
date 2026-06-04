using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaidOps.Application.Contracts.Characters.Commands;
using RaidOps.Application.Contracts.Characters.Queries;
using RaidOps.Application.Contracts.Characters.Responses;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using System.IdentityModel.Tokens.Jwt;

namespace RaidOps.API.Controllers.v1;

/// <summary>
/// Exposes Battle.net account linking endpoints via the BNet OAuth2 flow.
/// All routes require a valid JWT Bearer token.
/// </summary>
[ApiVersion("1.0")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class BnetController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    IConfiguration configuration) : ApiControllerBase(commandDispatcher, queryDispatcher)
{
    private static readonly HashSet<string> ValidRegions = ["us", "eu", "kr", "tw"];
    private readonly string _frontendUrl = configuration["FrontendUrl"] 
        ?? throw new InvalidOperationException("FrontendUrl is not configured");
    private readonly string _callbackUrl = configuration["BattleNet:CallbackUrl"]
        ?? throw new InvalidOperationException("BattleNet:CallbackUrl is not configured.");

    private string CallbackUrl => _callbackUrl;

    /// <summary>
    /// Returns the Battle.net account linked to the authenticated user, or 404 if not linked.
    /// </summary>
    [HttpGet("account")]
    public async Task<IActionResult> GetAccount(CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null) return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetBnetAccountQuery, BnetAccountResponse>(
            new GetBnetAccountQuery { UserDiscordId = discordId }, cancellationToken);

        if (result.IsFailed)
            return result.Error == ResponseDetail.NotFound ? NotFound() : BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }

    /// <summary>
    /// Generates the Battle.net OAuth2 authorization URL and redirects the user to it.
    /// </summary>
    [HttpGet("link/initiate")]
    public async Task<IActionResult> Initiate(
        [FromQuery] string region,
        CancellationToken cancellationToken = default)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null) return Unauthorized();

        if (!ValidRegions.Contains(region.ToLower()))
            return BadRequest("Invalid region. Must be one of: us, eu, kr, tw.");

        var result = await QueryDispatcher.DispatchAsync<GetBnetAuthorizationUrlQuery, string>(
            new GetBnetAuthorizationUrlQuery
            {
                DiscordId = discordId,
                Region = region.ToLower(),
                CallbackUrl = CallbackUrl,
            }, cancellationToken);

        return Redirect(result.Value!);
    }

    /// <summary>
    /// Handles the Battle.net OAuth2 callback: validates state, exchanges code,
    /// persists the linked account, then redirects to the front-end.
    /// </summary>
    [HttpGet("link/callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Redirect($"{_frontendUrl}/bnet-callback?error={ResponseDetail.Unauthorized}");

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            return Redirect($"{_frontendUrl}/bnet-callback?error={ResponseDetail.InvalidRequest}");

        var result = await CommandDispatcher.DispatchAsync(new HandleBnetCallbackCommand
        {
            DiscordId = discordId,
            Code = code,
            State = state,
            CallbackUrl = CallbackUrl
        }, cancellationToken);

        if (result.IsFailed)
            return Redirect($"{_frontendUrl}/bnet-callback?error={result.Error}");

        return Redirect($"{_frontendUrl}/bnet-callback?bnet_linked=true");
    }
}
