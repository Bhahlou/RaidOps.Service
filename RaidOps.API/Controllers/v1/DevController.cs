using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Dev.Commands;
using System.IdentityModel.Tokens.Jwt;

namespace RaidOps.API.Controllers.v1;

/// <summary>
/// Dev-only tooling endpoints. Every action 404s outside a Development environment, so nothing
/// here is reachable in staging/production even if a client somehow guesses the route.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/dev")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class DevController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    IHostEnvironment environment) : ApiControllerBase(commandDispatcher, queryDispatcher)
{
    /// <summary>
    /// Resets the calling user's onboarding progress for the specified guild: unlinks every
    /// Battle.net account (cascading to characters and guild memberships) and unregisters the
    /// guild, so the get-started flow can be replayed from scratch. Dev-only.
    /// </summary>
    [HttpPost("onboarding/reset")]
    public async Task<IActionResult> ResetOnboarding([FromQuery] string guildId, CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
            return NotFound();

        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await CommandDispatcher.DispatchAsync(
            new ResetGuildOnboardingCommand { UserDiscordId = discordId, GuildId = guildId },
            cancellationToken);

        return ToActionResult(result);
    }
}
