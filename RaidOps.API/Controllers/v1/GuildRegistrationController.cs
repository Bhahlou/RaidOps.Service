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

    /// <summary>
    /// Initiates the Discord bot OAuth2 flow for guild registration.
    /// Verifies that the authenticated user is an admin of the requested Discord guild,
    /// generates a signed CSRF state token, then redirects to the Discord authorization page.
    /// </summary>
    [HttpGet("register/initiate")]
    public IActionResult Initiate([FromQuery] string guildId)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var state = jwtService.GenerateStateToken(guildId, discordId);
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
            return Redirect($"{_frontendUrl}/no-guild?error=unauthorized");

        if (string.IsNullOrEmpty(state) || string.IsNullOrEmpty(guild_id))
            return Redirect($"{_frontendUrl}/no-guild?error=invalid_request");

        var stateData = jwtService.ValidateStateToken(state);
        if (stateData == null)
            return Redirect($"{_frontendUrl}/no-guild?error=invalid_state");

        if (stateData.Value.DiscordId != discordId || stateData.Value.GuildId != guild_id)
            return Redirect($"{_frontendUrl}/no-guild?error=state_mismatch");

        var result = await CommandDispatcher.DispatchAsync(new RegisterGuildCommand
        {
            GuildId = guild_id,
            RequesterDiscordId = discordId
        }, cancellationToken);

        if (result.IsFailed)
            return Redirect($"{_frontendUrl}/no-guild?error=register_failed");

        return Redirect($"{_frontendUrl}/guild-register/{guild_id}");
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
