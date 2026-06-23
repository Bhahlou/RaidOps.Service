using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Registration.Commands;
using RaidOps.Application.Contracts.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Web;

namespace RaidOps.API.Controllers.v1;

/// <summary>
/// Handles the Discord bot OAuth2 guild registration flow (initiate + callback).
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/guilds")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class GuildRegistrationController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    IJwtService jwtService,
    IConfiguration configuration) : ApiControllerBase(commandDispatcher, queryDispatcher)
{
    private readonly string _frontendUrl = configuration["FrontendUrl"]
        ?? throw new InvalidOperationException("FrontendUrl is not configured");
    private readonly string _discordClientId = configuration["Discord:ClientId"]
        ?? throw new InvalidOperationException("Discord:ClientId is not configured");
    private readonly long _botPermissions = long.TryParse(configuration["Discord:BotPermissions"], out var p)
        ? p
        : throw new InvalidOperationException("Discord:BotPermissions is not configured");

    /// <summary>Discriminators accepted for <see cref="Initiate"/>'s <c>returnTo</c> parameter.</summary>
    private static readonly HashSet<string> AllowedReturnTargets = ["get-started"];

    /// <summary>
    /// Initiates the Discord bot OAuth2 flow for guild registration.
    /// Verifies that the authenticated user is an admin of the requested Discord guild,
    /// generates a signed CSRF state token, then redirects to the Discord authorization page.
    /// </summary>
    [HttpGet("register/initiate")]
    public IActionResult Initiate([FromQuery] string guildId, [FromQuery] string? returnTo = null)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var safeReturnTo = returnTo != null && AllowedReturnTargets.Contains(returnTo) ? returnTo : null;

        var state = jwtService.GenerateStateToken(guildId, discordId, safeReturnTo);
        var callbackUrl = Url.Action(nameof(Callback), "GuildRegistration", new { version = "1.0" }, Request.Scheme)!;
        var discordUrl = BuildBotInviteUrl(guildId, callbackUrl, state);

        return Redirect(discordUrl);
    }

    /// <summary>
    /// Handles the Discord OAuth2 callback after the user authorizes the bot.
    /// Validates the CSRF state token, dispatches <see cref="RegisterGuildCommand"/>,
    /// and redirects the user to the guild registration completion page on success.
    /// </summary>
    [HttpGet("register/callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string? guild_id,
        [FromQuery] string? state,
        CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return RedirectToError("unauthorized", state);

        // guild_id is absent when the user cancels the bot-invite consent screen — state is
        // still echoed back by Discord in that case, so we can still recover returnTo from it.
        if (string.IsNullOrEmpty(state) || string.IsNullOrEmpty(guild_id))
            return RedirectToError("invalid_request", state);

        var stateData = jwtService.ValidateStateToken(state);
        if (stateData == null)
            return RedirectToError("invalid_state", state);

        if (stateData.Value.DiscordId != discordId || stateData.Value.GuildId != guild_id)
            return RedirectToError("state_mismatch", state);

        var result = await CommandDispatcher.DispatchAsync(new RegisterGuildCommand
        {
            GuildId = guild_id,
            RequesterDiscordId = discordId
        }, cancellationToken);

        if (result.IsFailed)
            return RedirectToError("register_failed", state);

        // The get-started stepper handles the settings step itself once the guild shows as
        // registered — no need to bounce through /guild-register first.
        if (stateData.Value.ReturnTo == "get-started")
            return Redirect($"{_frontendUrl}/get-started");

        return Redirect($"{_frontendUrl}/guild-register/{guild_id}");
    }

    /// <summary>
    /// Redirects to /no-guild with the given error, unless the original state token carries a
    /// recognized returnTo — in which case the user is sent back there instead, so a cancelled
    /// or failed registration started from onboarding doesn't strand the user on /no-guild.
    /// </summary>
    private IActionResult RedirectToError(string error, string? state)
    {
        var returnTo = state != null ? jwtService.ValidateStateToken(state)?.ReturnTo : null;
        var target = returnTo == "get-started" ? "get-started" : "no-guild";
        return Redirect($"{_frontendUrl}/{target}?error={error}");
    }

    private string BuildBotInviteUrl(string guildId, string redirectUri, string state)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["client_id"] = _discordClientId;
        query["scope"] = "bot";
        query["permissions"] = _botPermissions.ToString();
        query["guild_id"] = guildId;
        query["disable_guild_select"] = "true";
        query["response_type"] = "code";
        query["redirect_uri"] = redirectUri;
        query["state"] = state;
        return $"https://discord.com/oauth2/authorize?{query}";
    }
}
