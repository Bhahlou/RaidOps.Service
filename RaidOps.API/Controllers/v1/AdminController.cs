using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Spells.Commands;
using System.IdentityModel.Tokens.Jwt;

namespace RaidOps.API.Controllers.v1;

/// <summary>
/// Owner-only operational endpoints — gated on the requester's own Discord ID being listed in
/// <c>Admin:OwnerDiscordIds</c> rather than a guild-scoped permission, since these actions aren't
/// tied to any one guild. Still goes through the normal JWT cookie auth like every other
/// controller; there's no separate admin login.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class AdminController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    IConfiguration configuration) : ApiControllerBase(commandDispatcher, queryDispatcher)
{
    /// <summary>
    /// Re-syncs the <c>Spell</c> reference table from wago.tools right now, for every active
    /// wago-tracked branch — even branches already synced to their current build (<see cref="SyncSpellsCommand.Force"/>),
    /// so this is also useful to re-run after fixing a transform bug without waiting for a new build.
    /// </summary>
    [HttpPost("sync-spells")]
    public async Task<IActionResult> SyncSpells(CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var ownerDiscordIds = (configuration["Admin:OwnerDiscordIds"] ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (!ownerDiscordIds.Contains(discordId))
            return Forbid();

        var result = await CommandDispatcher.DispatchAsync(new SyncSpellsCommand { Force = true }, cancellationToken);
        return ToActionResult(result);
    }
}
