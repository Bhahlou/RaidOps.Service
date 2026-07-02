using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Notifications.Commands;
using System.IdentityModel.Tokens.Jwt;

namespace RaidOps.API.Controllers.v1;

/// <summary>
/// Exposes in-app notification endpoints: dismiss a derived notification.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notifications")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class NotificationsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ApiControllerBase(commandDispatcher, queryDispatcher)
{
    /// <summary>
    /// Records that the current user dismissed the given notification.
    /// </summary>
    [HttpPost("dismiss")]
    public async Task<IActionResult> Dismiss([FromBody] DismissNotificationCommand command, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.RequesterDiscordId = discordId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }
}
