using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Plans.Commands;
using RaidOps.Application.Contracts.Raids.Plans.Queries;
using RaidOps.Application.Contracts.Raids.Plans.Responses;
using System.IdentityModel.Tokens.Jwt;

namespace RaidOps.API.Controllers.v1;

/// <summary>
/// Manages a guild's raid strategy boards (raidplan.io-style: a background image with draggable
/// icons/text/shapes/arrows on top). Viewable at Roster level, editable Officer-only — see each
/// handler's access check.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/guilds")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class RaidPlansController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ApiControllerBase(commandDispatcher, queryDispatcher)
{
    /// <summary>Returns every strategy board the guild has for one boss.</summary>
    [HttpGet("{guildId}/raid-plans")]
    public async Task<IActionResult> GetPlansForBoss(string guildId, [FromQuery] int raidBossId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetRaidPlansForBossQuery, List<RaidPlanResponse>>(
            new GetRaidPlansForBossQuery { GuildId = guildId, RequesterDiscordId = discordId, RaidBossId = raidBossId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>Creates a new strategy board for a boss.</summary>
    [HttpPost("{guildId}/raid-plans")]
    public async Task<IActionResult> CreatePlan(string guildId, [FromBody] CreateRaidPlanCommand command, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.GuildId = guildId;
        command.RequesterDiscordId = discordId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Renames an existing strategy board.</summary>
    [HttpPatch("{guildId}/raid-plans/{raidPlanId:int}")]
    public async Task<IActionResult> RenamePlan(string guildId, int raidPlanId, [FromBody] RenameRaidPlanCommand command, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.GuildId = guildId;
        command.RequesterDiscordId = discordId;
        command.RaidPlanId = raidPlanId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Permanently deletes a strategy board, cascading its pages and elements.</summary>
    [HttpDelete("{guildId}/raid-plans/{raidPlanId:int}")]
    public async Task<IActionResult> DeletePlan(string guildId, int raidPlanId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await CommandDispatcher.DispatchAsync(
            new DeleteRaidPlanCommand { GuildId = guildId, RequesterDiscordId = discordId, RaidPlanId = raidPlanId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>Returns a board's page-tab list (no elements).</summary>
    [HttpGet("{guildId}/raid-plans/{raidPlanId:int}/pages")]
    public async Task<IActionResult> GetPages(string guildId, int raidPlanId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetRaidPlanPagesQuery, List<RaidPlanPageResponse>>(
            new GetRaidPlanPagesQuery { GuildId = guildId, RequesterDiscordId = discordId, RaidPlanId = raidPlanId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>Adds a new page to a board, appended after its current last page.</summary>
    [HttpPost("{guildId}/raid-plans/{raidPlanId:int}/pages")]
    public async Task<IActionResult> CreatePage(string guildId, int raidPlanId, [FromBody] CreateRaidPlanPageCommand command, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.GuildId = guildId;
        command.RequesterDiscordId = discordId;
        command.RaidPlanId = raidPlanId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Returns one page including its elements — the canvas editor's main load call.</summary>
    [HttpGet("{guildId}/raid-plans/{raidPlanId:int}/pages/{pageId:int}")]
    public async Task<IActionResult> GetPageDetail(string guildId, int raidPlanId, int pageId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetRaidPlanPageDetailQuery, RaidPlanPageDetailResponse>(
            new GetRaidPlanPageDetailQuery { GuildId = guildId, RequesterDiscordId = discordId, RaidPlanId = raidPlanId, RaidPlanPageId = pageId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>Renames an existing page.</summary>
    [HttpPatch("{guildId}/raid-plans/{raidPlanId:int}/pages/{pageId:int}")]
    public async Task<IActionResult> RenamePage(string guildId, int raidPlanId, int pageId, [FromBody] RenameRaidPlanPageCommand command, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.GuildId = guildId;
        command.RequesterDiscordId = discordId;
        command.RaidPlanId = raidPlanId;
        command.RaidPlanPageId = pageId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Permanently deletes a page, cascading its elements.</summary>
    [HttpDelete("{guildId}/raid-plans/{raidPlanId:int}/pages/{pageId:int}")]
    public async Task<IActionResult> DeletePage(string guildId, int raidPlanId, int pageId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await CommandDispatcher.DispatchAsync(
            new DeleteRaidPlanPageCommand { GuildId = guildId, RequesterDiscordId = discordId, RaidPlanId = raidPlanId, RaidPlanPageId = pageId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>Re-numbers a board's pages to match the given order.</summary>
    [HttpPost("{guildId}/raid-plans/{raidPlanId:int}/pages/reorder")]
    public async Task<IActionResult> ReorderPages(string guildId, int raidPlanId, [FromBody] ReorderRaidPlanPagesCommand command, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.GuildId = guildId;
        command.RequesterDiscordId = discordId;
        command.RaidPlanId = raidPlanId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Sets or clears a page's background image.</summary>
    [HttpPost("{guildId}/raid-plans/{raidPlanId:int}/pages/{pageId:int}/background")]
    public async Task<IActionResult> SetPageBackground(string guildId, int raidPlanId, int pageId, [FromBody] SetRaidPlanPageBackgroundCommand command, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.GuildId = guildId;
        command.RequesterDiscordId = discordId;
        command.RaidPlanId = raidPlanId;
        command.RaidPlanPageId = pageId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Replaces every element of a page with the given list — the canvas editor's Save button.</summary>
    [HttpPost("{guildId}/raid-plans/{raidPlanId:int}/pages/{pageId:int}/elements")]
    public async Task<IActionResult> SavePageElements(string guildId, int raidPlanId, int pageId, [FromBody] SaveRaidPlanPageElementsCommand command, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.GuildId = guildId;
        command.RequesterDiscordId = discordId;
        command.RaidPlanId = raidPlanId;
        command.RaidPlanPageId = pageId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }
}
