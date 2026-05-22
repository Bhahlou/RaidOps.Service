using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Commands;
using RaidOps.Application.Contracts.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Web;

namespace RaidOps.API.Controllers.v1;

/// <summary>
/// Exposes guild-management endpoints, including the Discord bot OAuth2 registration flow.
/// All routes require a valid JWT Bearer token.
/// </summary>
[ApiVersion("1.0")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class GuildsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    IJwtService jwtService,
    IConfiguration configuration) : ApiControllerBase(commandDispatcher, queryDispatcher)
{
    private readonly string _frontendUrl = configuration["FrontendUrl"] ?? "http://localhost:4200";
    private readonly string _discordClientId = configuration["Discord:ClientId"] ?? string.Empty;
    private readonly long _botPermissions = long.TryParse(configuration["Discord:BotPermissions"], out var p) ? p : 0;

    /// <summary>
    /// Initiates the Discord bot OAuth2 flow for guild registration.
    /// Verifies that the authenticated user is an admin of the requested Discord guild,
    /// generates a signed CSRF state token, then redirects to the Discord authorization page.
    /// </summary>
    /// <param name="guildId">The Discord snowflake ID of the guild to register.</param>
    /// <returns>
    /// A 302 redirect to Discord's bot authorization URL on success,
    /// or <c>401 Unauthorized</c> / <c>403 Forbidden</c> if the user is not an admin of the guild.
    /// </returns>
    [HttpGet("register/initiate")]
    public IActionResult Initiate([FromQuery] string guildId)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var state = jwtService.GenerateStateToken(guildId, discordId);
        var callbackUrl = Url.Action(nameof(Callback), "Guilds", new { version = "1.0" }, Request.Scheme)!;
        var discordUrl = BuildBotInviteUrl(guildId, callbackUrl, state);

        return Redirect(discordUrl);
    }

    /// <summary>
    /// Handles the Discord OAuth2 callback after the user authorizes the bot.
    /// Validates the CSRF state token, dispatches <see cref="RegisterGuildCommand"/>,
    /// and redirects the user to the guild dashboard on success.
    /// </summary>
    /// <param name="guild_id">The Discord snowflake ID of the guild the bot was added to (provided by Discord).</param>
    /// <param name="state">The signed CSRF state token generated during <see cref="Initiate"/>.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// A 302 redirect to the guild dashboard on success,
    /// or a redirect to <c>/no-guild?error=…</c> on failure.
    /// </returns>
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

        return Redirect($"{_frontendUrl}/guilds/{guild_id}/dashboard");
    }

    /// <summary>
    /// Builds the Discord bot authorization URL with the required OAuth2 parameters.
    /// </summary>
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
