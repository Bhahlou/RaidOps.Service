using Asp.Versioning;
using AspNet.Security.OAuth.Discord;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using RaidOps.Application.Contracts.Authentication.Commands;
using RaidOps.Application.Contracts.Authentication.Responses;
using RaidOps.Application.Contracts.CQRS;
using System.Security.Claims;

namespace RaidOps.API.Controllers.v1;

/// <summary>
/// Exposes the Discord OAuth2 authentication endpoints: initiate sign-up,
/// handle the OAuth2 callback, refresh tokens, and log out.
/// </summary>
[ApiVersion("1.0")]
public class DiscordAuthController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    IConfiguration configuration) : ApiControllerBase(commandDispatcher, queryDispatcher)
{
    private readonly string _frontendUrl = configuration["FrontendUrl"]
        ?? throw new InvalidOperationException("FrontendUrl is not configured");
    private const string ACCESS_TOKEN = "access_token";
    private const string REFRESH_TOKEN = "refresh_token";

    /// <summary>Discriminators accepted for <see cref="Signup"/>'s <c>returnTo</c> parameter — the
    /// two pages the Discord login challenge can be triggered from.</summary>
    private static readonly HashSet<string> AllowedReturnTargets = ["home", "get-started"];

    /// <summary>
    /// Initiates the Discord OAuth2 sign-up flow by issuing a challenge that redirects
    /// the user to the Discord authorization page. <paramref name="returnTo"/> is carried
    /// through the OAuth state so that if the user cancels on Discord's consent screen,
    /// <see cref="Program"/>'s <c>OnRemoteFailure</c> handler can send them back to the
    /// page they started from instead of a fixed page.
    /// </summary>
    /// <returns>A challenge result targeting the Discord authentication scheme.</returns>
    [HttpGet("signup")]
    public IActionResult Signup([FromQuery] string? returnTo = null)
    {
        var safeReturnTo = returnTo != null && AllowedReturnTargets.Contains(returnTo) ? returnTo : "home";
        var callbackUrl = Url.Action(nameof(SignupCallback), "DiscordAuth", new { version = "1.0" }, Request.Scheme);
        var properties = new AuthenticationProperties { RedirectUri = callbackUrl };
        properties.Items["returnTo"] = safeReturnTo;
        return Challenge(properties, DiscordAuthenticationDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Handles the Discord OAuth2 callback after the user grants authorization.
    /// Extracts the Discord tokens, dispatches a <see cref="SignupCommand"/>, sets HttpOnly
    /// auth cookies, and redirects the user back to the frontend.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// A redirect to the frontend <c>/authcallback</c> route on success,
    /// or <c>401 Unauthorized</c> / <c>400 Bad Request</c> on failure.
    /// </returns>
    [HttpGet("signupCallback")]
    public async Task<IActionResult> SignupCallback(CancellationToken cancellationToken)
    {
        var authenticateResult = await HttpContext.AuthenticateAsync();
        if (!authenticateResult.Succeeded || authenticateResult.Principal == null)
            return Unauthorized("Authentication failed.");

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        Response.Cookies.Delete(".RaidOps.Auth");

        var claims = authenticateResult.Principal.Claims.ToList();
        var discordId = claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value;
        var discordAccessToken = await HttpContext.GetTokenAsync(ACCESS_TOKEN);
        var discordRefreshToken = await HttpContext.GetTokenAsync(REFRESH_TOKEN);

        if (string.IsNullOrEmpty(discordAccessToken) || string.IsNullOrEmpty(discordRefreshToken))
            return Unauthorized("Discord tokens are missing.");

        var command = new SignupCommand
        {
            DiscordId = discordId,
            DiscordAccessToken = discordAccessToken,
            DiscordRefreshToken = discordRefreshToken
        };

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);

        if (result.IsFailed || result.Value!.Body is not AuthenticationResponse authResp)
            return BadRequest(result.Error);

        AppendAuthCookies(authResp);
        return Redirect($"{_frontendUrl}/authcallback");
    }

    /// <summary>
    /// Validates the <c>refresh_token</c> cookie, re-syncs Discord data, and issues
    /// a new access/refresh token pair by dispatching a <see cref="RefreshTokenCommand"/>.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// <c>200 OK</c> with new auth cookies on success, or <c>401 Unauthorized</c> on failure.
    /// </returns>
    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken(CancellationToken cancellationToken)
    {
        var refreshJwt = Request.Cookies[REFRESH_TOKEN];
        if (refreshJwt == null)
            return Unauthorized();

        var result = await CommandDispatcher.DispatchAsync(
            new RefreshTokenCommand { RefreshToken = refreshJwt },
            cancellationToken);

        if (result.IsFailed || result.Value!.Body is not AuthenticationResponse authResp)
            return Unauthorized();

        AppendAuthCookies(authResp);
        return Ok();
    }

    /// <summary>
    /// Clears the <c>access_token</c> and <c>refresh_token</c> cookies, effectively
    /// logging the user out of the current device.
    /// </summary>
    /// <returns><c>200 OK</c>.</returns>
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(ACCESS_TOKEN);
        Response.Cookies.Delete(REFRESH_TOKEN);
        return Ok();
    }

    /// <summary>
    /// Writes the access and refresh tokens from <paramref name="authResp"/> into
    /// HttpOnly, Secure, SameSite=Lax cookies with the appropriate expiry times.
    /// Lax (not None) since the front end and API are always same-site (subdomains of the
    /// same registrable domain in every environment) — this blocks cross-site "simple request"
    /// CSRF (e.g. a forged auto-submitting form POST) without affecting legitimate same-site calls.
    /// </summary>
    /// <param name="authResp">The authentication response containing tokens and their expiry times.</param>
    private void AppendAuthCookies(AuthenticationResponse authResp)
    {
        Response.Cookies.Append(ACCESS_TOKEN, authResp.AccessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure   = true,
            SameSite = SameSiteMode.Lax,
            Expires  = authResp.AccessTokenExpiration,
        });
        Response.Cookies.Append(REFRESH_TOKEN, authResp.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure   = true,
            SameSite = SameSiteMode.Lax,
            Expires  = authResp.RefreshTokenExpiration,
        });
    }
}
