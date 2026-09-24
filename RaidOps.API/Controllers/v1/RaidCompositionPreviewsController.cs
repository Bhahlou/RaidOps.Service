using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.CompositionPreviews.Commands;
using RaidOps.Application.Contracts.Raids.CompositionPreviews.Queries;
using RaidOps.Application.Contracts.Raids.CompositionPreviews.Responses;
using System.IdentityModel.Tokens.Jwt;

namespace RaidOps.API.Controllers.v1;

/// <summary>
/// Manages a guild branch's raid composition previews — officer-authored, theoretical class/spec
/// grids built ahead of real roster data. Every route is scoped to a single guild branch, same as
/// <c>RaidsController</c>. Access gating (Officer-only, both read and write) happens inside each
/// command/query handler via <c>IGuildAccessService</c>'s branch-scoped overload, not through a
/// controller-level attribute — the same pattern as every other guild-scoped controller.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/guilds")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class RaidCompositionPreviewsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ApiControllerBase(commandDispatcher, queryDispatcher)
{
    /// <summary>Returns every raid composition preview of the guild branch, most recently updated first.</summary>
    [HttpGet("{guildId}/branches/{guildBranchId:int}/composition-previews")]
    public async Task<IActionResult> GetPreviews(string guildId, int guildBranchId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetRaidCompositionPreviewsQuery, List<RaidCompositionPreviewSummaryResponse>>(
            new GetRaidCompositionPreviewsQuery { GuildId = guildId, GuildBranchId = guildBranchId, RequesterDiscordId = discordId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>Returns a single raid composition preview with its full slot grid — backs the composer page.</summary>
    [HttpGet("{guildId}/branches/{guildBranchId:int}/composition-previews/{previewId:int}")]
    public async Task<IActionResult> GetPreview(string guildId, int guildBranchId, int previewId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetRaidCompositionPreviewQuery, RaidCompositionPreviewResponse>(
            new GetRaidCompositionPreviewQuery { GuildId = guildId, GuildBranchId = guildBranchId, PreviewId = previewId, RequesterDiscordId = discordId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>Creates a new raid composition preview with an empty grid sized from the requested format.</summary>
    [HttpPost("{guildId}/branches/{guildBranchId:int}/composition-previews")]
    public async Task<IActionResult> CreatePreview(string guildId, int guildBranchId, [FromBody] CreateRaidCompositionPreviewCommand command, CancellationToken cancellationToken)
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

    /// <summary>Renames an existing raid composition preview.</summary>
    [HttpPatch("{guildId}/branches/{guildBranchId:int}/composition-previews/{previewId:int}")]
    public async Task<IActionResult> RenamePreview(string guildId, int guildBranchId, int previewId, [FromBody] RenameRaidCompositionPreviewCommand command, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.GuildId = guildId;
        command.GuildBranchId = guildBranchId;
        command.RequesterDiscordId = discordId;
        command.PreviewId = previewId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Clones an existing raid composition preview — its format/grid shape and every slot — under a new name.</summary>
    [HttpPost("{guildId}/branches/{guildBranchId:int}/composition-previews/{previewId:int}/duplicate")]
    public async Task<IActionResult> DuplicatePreview(string guildId, int guildBranchId, int previewId, [FromBody] DuplicateRaidCompositionPreviewCommand command, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.GuildId = guildId;
        command.GuildBranchId = guildBranchId;
        command.RequesterDiscordId = discordId;
        command.PreviewId = previewId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Permanently deletes a raid composition preview, including all of its slots.</summary>
    [HttpDelete("{guildId}/branches/{guildBranchId:int}/composition-previews/{previewId:int}")]
    public async Task<IActionResult> DeletePreview(string guildId, int guildBranchId, int previewId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await CommandDispatcher.DispatchAsync(
            new DeleteRaidCompositionPreviewCommand { GuildId = guildId, GuildBranchId = guildBranchId, RequesterDiscordId = discordId, PreviewId = previewId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>Upserts (or, if left fully empty, clears) a single (group, slot) coordinate of a preview's grid.</summary>
    [HttpPatch("{guildId}/branches/{guildBranchId:int}/composition-previews/{previewId:int}/slots")]
    public async Task<IActionResult> UpdateSlot(string guildId, int guildBranchId, int previewId, [FromBody] UpdateRaidCompositionPreviewSlotCommand command, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.GuildId = guildId;
        command.GuildBranchId = guildBranchId;
        command.RequesterDiscordId = discordId;
        command.PreviewId = previewId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }
}
