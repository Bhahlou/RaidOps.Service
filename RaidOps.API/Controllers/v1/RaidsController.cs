using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Assignments.Commands;
using RaidOps.Application.Contracts.Raids.Events.Commands;
using RaidOps.Application.Contracts.Raids.Events.Queries;
using RaidOps.Application.Contracts.Raids.Events.Responses;
using RaidOps.Application.Contracts.Raids.Roster.Queries;
using RaidOps.Application.Contracts.Raids.Roster.Responses;
using RaidOps.Application.Contracts.Raids.Series.Commands;
using RaidOps.Application.Contracts.Raids.Series.Queries;
using RaidOps.Application.Contracts.Raids.Series.Responses;
using RaidOps.Application.Contracts.Raids.Zones.Queries;
using RaidOps.Application.Contracts.Raids.Zones.Responses;
using System.IdentityModel.Tokens.Jwt;

namespace RaidOps.API.Controllers.v1;

/// <summary>
/// Manages a guild's raid builder: zone lookup, recurring series, concrete events, on-demand
/// occurrence materialization, and the sparse group/slot assignment grid. Access gating (Roster
/// vs Officer) happens inside each command/query handler via <c>IGuildAccessService</c>, not
/// through a controller-level attribute — the same pattern as every other guild-scoped controller.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/guilds")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class RaidsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ApiControllerBase(commandDispatcher, queryDispatcher)
{
    /// <summary>Returns the raid zones available on the currently active expansion of the given branch.</summary>
    [HttpGet("{guildId}/raids/zones")]
    public async Task<IActionResult> GetZonesForBranch(string guildId, [FromQuery] int branchId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetRaidZonesForBranchQuery, List<RaidZoneResponse>>(
            new GetRaidZonesForBranchQuery { GuildId = guildId, RequesterDiscordId = discordId, BranchId = branchId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>Returns every recurring raid series (active or not) of the guild.</summary>
    [HttpGet("{guildId}/raids/series")]
    public async Task<IActionResult> GetSeriesList(string guildId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetRaidSeriesListQuery, List<RaidSeriesResponse>>(
            new GetRaidSeriesListQuery { GuildId = guildId, RequesterDiscordId = discordId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>Creates a new recurring raid series.</summary>
    [HttpPost("{guildId}/raids/series")]
    public async Task<IActionResult> CreateSeries(string guildId, [FromBody] CreateRaidSeriesCommand command, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.GuildId = guildId;
        command.RequesterDiscordId = discordId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Updates an existing recurring raid series' settings and default zones.</summary>
    [HttpPatch("{guildId}/raids/series/{seriesId:int}")]
    public async Task<IActionResult> UpdateSeries(string guildId, int seriesId, [FromBody] UpdateRaidSeriesCommand command, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.GuildId = guildId;
        command.RequesterDiscordId = discordId;
        command.SeriesId = seriesId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Stops future materialization of a recurring raid series without touching its past occurrences.</summary>
    [HttpPost("{guildId}/raids/series/{seriesId:int}/deactivate")]
    public async Task<IActionResult> DeactivateSeries(string guildId, int seriesId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await CommandDispatcher.DispatchAsync(
            new DeactivateRaidSeriesCommand { GuildId = guildId, RequesterDiscordId = discordId, SeriesId = seriesId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>Idempotently materializes concrete raid events for every active series over a date range.</summary>
    [HttpPost("{guildId}/raids/materialize")]
    public async Task<IActionResult> MaterializeOccurrences(
        string guildId,
        [FromQuery] DateOnly rangeStart,
        [FromQuery] DateOnly rangeEnd,
        CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await CommandDispatcher.DispatchAsync(
            new MaterializeRaidSeriesOccurrencesCommand { GuildId = guildId, RequesterDiscordId = discordId, RangeStart = rangeStart, RangeEnd = rangeEnd },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>Returns every raid event of the guild within a date range, with target zones and slot assignments.</summary>
    [HttpGet("{guildId}/raids/board")]
    public async Task<IActionResult> GetBoard(
        string guildId,
        [FromQuery] DateOnly rangeStart,
        [FromQuery] DateOnly rangeEnd,
        CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetRaidBoardQuery, RaidBoardResponse>(
            new GetRaidBoardQuery { GuildId = guildId, RequesterDiscordId = discordId, RangeStart = rangeStart, RangeEnd = rangeEnd },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>Creates a standalone raid event, not tied to any recurring series.</summary>
    [HttpPost("{guildId}/raids/events")]
    public async Task<IActionResult> CreateEvent(string guildId, [FromBody] CreateAdhocRaidEventCommand command, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.GuildId = guildId;
        command.RequesterDiscordId = discordId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Updates a raid event's schedule and target-zone set.</summary>
    [HttpPatch("{guildId}/raids/events/{eventId:int}")]
    public async Task<IActionResult> UpdateEvent(string guildId, int eventId, [FromBody] UpdateRaidEventCommand command, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.GuildId = guildId;
        command.RequesterDiscordId = discordId;
        command.EventId = eventId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Permanently deletes a raid event that has no slot assignments.</summary>
    [HttpDelete("{guildId}/raids/events/{eventId:int}")]
    public async Task<IActionResult> DeleteEvent(string guildId, int eventId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await CommandDispatcher.DispatchAsync(
            new DeleteRaidEventCommand { GuildId = guildId, RequesterDiscordId = discordId, EventId = eventId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>Cancels a raid event, preserving its assignments and history.</summary>
    [HttpPost("{guildId}/raids/events/{eventId:int}/cancel")]
    public async Task<IActionResult> CancelEvent(string guildId, int eventId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await CommandDispatcher.DispatchAsync(
            new CancelRaidEventCommand { GuildId = guildId, RequesterDiscordId = discordId, EventId = eventId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>Publishes a raid event, making it visible to non-officer roster members.</summary>
    [HttpPost("{guildId}/raids/events/{eventId:int}/publish")]
    public async Task<IActionResult> PublishEvent(string guildId, int eventId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await CommandDispatcher.DispatchAsync(
            new PublishRaidEventCommand { GuildId = guildId, RequesterDiscordId = discordId, EventId = eventId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>Assigns a character to a (group, slot) coordinate of a raid event's grid.</summary>
    [HttpPost("{guildId}/raids/events/{eventId:int}/slots/assign")]
    public async Task<IActionResult> AssignSlot(string guildId, int eventId, [FromBody] AssignCharacterToSlotCommand command, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.GuildId = guildId;
        command.RequesterDiscordId = discordId;
        command.EventId = eventId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Clears a (group, slot) coordinate of a raid event's grid.</summary>
    [HttpPost("{guildId}/raids/events/{eventId:int}/slots/unassign")]
    public async Task<IActionResult> UnassignSlot(string guildId, int eventId, [FromBody] UnassignSlotCommand command, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.GuildId = guildId;
        command.RequesterDiscordId = discordId;
        command.EventId = eventId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Returns every active roster character not assigned to any raid event within a date range.</summary>
    [HttpGet("{guildId}/raids/unassigned-members")]
    public async Task<IActionResult> GetUnassignedMembers(
        string guildId,
        [FromQuery] DateOnly rangeStart,
        [FromQuery] DateOnly rangeEnd,
        CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetUnassignedGuildMembersQuery, List<UnassignedMemberResponse>>(
            new GetUnassignedGuildMembersQuery { GuildId = guildId, RequesterDiscordId = discordId, RangeStart = rangeStart, RangeEnd = rangeEnd },
            cancellationToken);

        return ToActionResult(result);
    }
}
