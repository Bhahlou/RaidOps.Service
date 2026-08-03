using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaidOps.Application.Contracts.Authentication.Commands;
using RaidOps.Application.Contracts.Authentication.Queries;
using RaidOps.Application.Contracts.Authentication.Responses;
using RaidOps.Application.Contracts.CQRS;
using System.IdentityModel.Tokens.Jwt;

namespace RaidOps.API.Controllers.v1;

/// <summary>
/// Provides endpoints for querying and updating the authenticated user's own profile.
/// All routes require a valid JWT Bearer token.
/// </summary>
[ApiVersion("1.0")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class UserController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ApiControllerBase(commandDispatcher, queryDispatcher)
{
    /// <summary>
    /// Returns the profile of the currently authenticated user by extracting their
    /// Discord ID from the JWT <c>sub</c> claim and dispatching a <see cref="GetMeQuery"/>.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// <c>200 OK</c> with a <see cref="UserResponse"/> on success,
    /// <c>401 Unauthorized</c> if the <c>sub</c> claim is missing,
    /// or <c>400 Bad Request</c> if the user is not found.
    /// </returns>
    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetMeQuery, UserResponse>(
            new GetMeQuery { DiscordId = discordId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>
    /// Records that the current user has acknowledged a changelog entry, keeping
    /// "what's new" state in sync across devices.
    /// </summary>
    [HttpPost("changelog-seen")]
    public async Task<IActionResult> MarkChangelogSeen([FromBody] MarkChangelogSeenCommand command, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.RequesterDiscordId = discordId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }
}
