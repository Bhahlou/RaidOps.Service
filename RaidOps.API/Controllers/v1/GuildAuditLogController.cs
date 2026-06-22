using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.AuditLog.Queries;
using RaidOps.Application.Contracts.Guilds.AuditLog.Responses;
using RaidOps.Domain.Enums;
using System.IdentityModel.Tokens.Jwt;

namespace RaidOps.API.Controllers.v1;

/// <summary>
/// Exposes read access to a guild's audit log.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/guilds")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class GuildAuditLogController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ApiControllerBase(commandDispatcher, queryDispatcher)
{
    /// <summary>
    /// Returns a page of the specified guild's audit log, newest-first.
    /// </summary>
    [HttpGet("{guildId}/audit-log")]
    public async Task<IActionResult> GetAuditLog(
        string guildId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] GuildAuditAction? actionType = null,
        [FromQuery] GuildAuditCategory? category = null,
        CancellationToken cancellationToken = default)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetGuildAuditLogQuery, GuildAuditLogPageResponse>(
            new GetGuildAuditLogQuery
            {
                GuildId = guildId,
                RequesterDiscordId = discordId,
                Page = page,
                PageSize = pageSize,
                ActionType = actionType,
                Category = category,
            },
            cancellationToken);

        return ToActionResult(result);
    }
}
