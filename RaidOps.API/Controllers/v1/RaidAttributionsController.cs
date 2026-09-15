using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Attributions.Commands;
using RaidOps.Application.Contracts.Raids.Attributions.Queries;
using RaidOps.Application.Contracts.Raids.Attributions.Responses;
using System.IdentityModel.Tokens.Jwt;

namespace RaidOps.API.Controllers.v1;

/// <summary>Manages a raid event's Assignments tab — filling/clearing the guild's attribution template rows with characters seated in that event.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/guilds")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class RaidAttributionsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ApiControllerBase(commandDispatcher, queryDispatcher)
{
    /// <summary>Returns the guild's attribution template merged with this event's existing fills and seated characters.</summary>
    [HttpGet("{guildId}/branches/{guildBranchId:int}/raids/events/{eventId:int}/attributions")]
    public async Task<IActionResult> GetAttributions(string guildId, int guildBranchId, int eventId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetRaidEventAttributionsQuery, RaidEventAttributionsResponse>(
            new GetRaidEventAttributionsQuery { GuildId = guildId, GuildBranchId = guildBranchId, EventId = eventId, RequesterDiscordId = discordId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>Fills one slot of a raid event's attributions.</summary>
    [HttpPost("{guildId}/branches/{guildBranchId:int}/raids/events/{eventId:int}/attributions/set")]
    public async Task<IActionResult> SetAttribution(string guildId, int guildBranchId, int eventId, [FromBody] SetRaidEventAttributionCommand command, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.GuildId = guildId;
        command.GuildBranchId = guildBranchId;
        command.EventId = eventId;
        command.RequesterDiscordId = discordId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Clears one slot of a raid event's attributions.</summary>
    [HttpPost("{guildId}/branches/{guildBranchId:int}/raids/events/{eventId:int}/attributions/clear")]
    public async Task<IActionResult> ClearAttribution(string guildId, int guildBranchId, int eventId, [FromBody] ClearRaidEventAttributionCommand command, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.GuildId = guildId;
        command.GuildBranchId = guildBranchId;
        command.EventId = eventId;
        command.RequesterDiscordId = discordId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }
}
