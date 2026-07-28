using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Branches.Commands;
using RaidOps.Application.Contracts.Guilds.Branches.Queries;
using RaidOps.Application.Contracts.Guilds.Branches.Responses;
using System.IdentityModel.Tokens.Jwt;

namespace RaidOps.API.Controllers.v1;

/// <summary>
/// Exposes per-guild WoW branch activation and roster/officer role-set configuration —
/// replacing the old guild-wide roster mode and role thresholds.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/guilds")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class GuildBranchesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ApiControllerBase(commandDispatcher, queryDispatcher)
{
    /// <summary>
    /// Returns every WoW branch activated on the specified guild (active and deactivated).
    /// </summary>
    [HttpGet("{guildId}/branches")]
    public async Task<IActionResult> GetBranches(string guildId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetGuildBranchesQuery, List<GuildBranchResponse>>(
            new GetGuildBranchesQuery { GuildId = guildId, RequesterDiscordId = discordId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>
    /// Activates a WoW branch on the specified guild.
    /// </summary>
    [HttpPost("{guildId}/branches")]
    public async Task<IActionResult> ActivateBranch(
        string guildId,
        [FromBody] ActivateGuildBranchCommand command,
        CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.GuildId = guildId;
        command.RequesterDiscordId = discordId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Deactivates a guild branch. Never hard-deletes — roster history and role-set configuration
    /// are preserved for a future reactivation.
    /// </summary>
    [HttpDelete("{guildId}/branches/{guildBranchId:int}")]
    public async Task<IActionResult> DeactivateBranch(string guildId, int guildBranchId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await CommandDispatcher.DispatchAsync(
            new DeactivateGuildBranchCommand { GuildId = guildId, GuildBranchId = guildBranchId, RequesterDiscordId = discordId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>
    /// Persists the roster/officer role-set configuration for one guild branch.
    /// </summary>
    [HttpPatch("{guildId}/branches/{guildBranchId:int}/roster-settings")]
    public async Task<IActionResult> UpdateRosterSettings(
        string guildId,
        int guildBranchId,
        [FromBody] UpdateGuildBranchRosterSettingsCommand command,
        CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.GuildId = guildId;
        command.GuildBranchId = guildBranchId;
        command.RequesterDiscordId = discordId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }
}
