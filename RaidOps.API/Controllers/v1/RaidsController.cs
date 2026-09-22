using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Assignments.Commands;
using RaidOps.Application.Contracts.Raids.Events.Commands;
using RaidOps.Application.Contracts.Raids.Events.Queries;
using RaidOps.Application.Contracts.Raids.Events.Responses;
using RaidOps.Application.Contracts.Raids.Lockout.Queries;
using RaidOps.Application.Contracts.Raids.Lockout.Responses;
using RaidOps.Application.Contracts.Raids.Roster.Queries;
using RaidOps.Application.Contracts.Raids.Roster.Responses;
using RaidOps.Application.Contracts.Raids.Series.Commands;
using RaidOps.Application.Contracts.Raids.Series.Queries;
using RaidOps.Application.Contracts.Raids.Series.Responses;
using RaidOps.Application.Contracts.Raids.Signups.Commands;
using RaidOps.Application.Contracts.Raids.Signups.Queries;
using RaidOps.Application.Contracts.Raids.Signups.Responses;
using RaidOps.Application.Contracts.Raids.Zones.Queries;
using RaidOps.Application.Contracts.Raids.Zones.Responses;
using System.IdentityModel.Tokens.Jwt;

namespace RaidOps.API.Controllers.v1;

/// <summary>
/// Manages a guild branch's raid builder: zone lookup, recurring series, concrete events, on-demand
/// occurrence materialization, and the sparse group/slot assignment grid. Every route is scoped to a
/// single guild branch (<c>guildBranchId</c>) — a raid series/event belongs to exactly one branch,
/// same as roster/dashboard/loot. Access gating (Roster vs Officer) happens inside each command/query
/// handler via <c>IGuildAccessService</c>'s branch-scoped overload, not through a controller-level
/// attribute — the same pattern as every other guild-scoped controller.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/guilds")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class RaidsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ApiControllerBase(commandDispatcher, queryDispatcher)
{
    /// <summary>Returns the raid zones available on the currently active expansion of the given guild branch.</summary>
    [HttpGet("{guildId}/branches/{guildBranchId:int}/raids/zones")]
    public async Task<IActionResult> GetZonesForBranch(string guildId, int guildBranchId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetRaidZonesForBranchQuery, List<RaidZoneResponse>>(
            new GetRaidZonesForBranchQuery { GuildId = guildId, GuildBranchId = guildBranchId, RequesterDiscordId = discordId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>Returns the current weekly raid-lockout window of the guild branch, or a fully-null response if its region isn't configured yet.</summary>
    [HttpGet("{guildId}/branches/{guildBranchId:int}/raids/lockout-week")]
    public async Task<IActionResult> GetLockoutWeek(string guildId, int guildBranchId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetGuildBranchLockoutWeekQuery, GuildBranchLockoutWeekResponse>(
            new GetGuildBranchLockoutWeekQuery { GuildId = guildId, GuildBranchId = guildBranchId, RequesterDiscordId = discordId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>Returns every recurring raid series (active or not) of the guild branch.</summary>
    [HttpGet("{guildId}/branches/{guildBranchId:int}/raids/series")]
    public async Task<IActionResult> GetSeriesList(string guildId, int guildBranchId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetRaidSeriesListQuery, List<RaidSeriesResponse>>(
            new GetRaidSeriesListQuery { GuildId = guildId, GuildBranchId = guildBranchId, RequesterDiscordId = discordId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>Creates a new recurring raid series.</summary>
    [HttpPost("{guildId}/branches/{guildBranchId:int}/raids/series")]
    public async Task<IActionResult> CreateSeries(string guildId, int guildBranchId, [FromBody] CreateRaidSeriesCommand command, CancellationToken cancellationToken)
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

    /// <summary>Updates an existing recurring raid series' settings and default zones.</summary>
    [HttpPatch("{guildId}/branches/{guildBranchId:int}/raids/series/{seriesId:int}")]
    public async Task<IActionResult> UpdateSeries(string guildId, int guildBranchId, int seriesId, [FromBody] UpdateRaidSeriesCommand command, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.GuildId = guildId;
        command.GuildBranchId = guildBranchId;
        command.RequesterDiscordId = discordId;
        command.SeriesId = seriesId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Stops future materialization of a recurring raid series, optionally bulk-deleting its empty unpublished occurrences too.</summary>
    [HttpPost("{guildId}/branches/{guildBranchId:int}/raids/series/{seriesId:int}/deactivate")]
    public async Task<IActionResult> DeactivateSeries(string guildId, int guildBranchId, int seriesId, [FromBody] DeactivateRaidSeriesCommand command, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.GuildId = guildId;
        command.GuildBranchId = guildBranchId;
        command.RequesterDiscordId = discordId;
        command.SeriesId = seriesId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Idempotently materializes concrete raid events for every active series over a date range.</summary>
    [HttpPost("{guildId}/branches/{guildBranchId:int}/raids/materialize")]
    public async Task<IActionResult> MaterializeOccurrences(
        string guildId,
        int guildBranchId,
        [FromQuery] DateOnly rangeStart,
        [FromQuery] DateOnly rangeEnd,
        CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await CommandDispatcher.DispatchAsync(
            new MaterializeRaidSeriesOccurrencesCommand { GuildId = guildId, GuildBranchId = guildBranchId, RequesterDiscordId = discordId, RangeStart = rangeStart, RangeEnd = rangeEnd },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>Returns every raid event of the guild branch within a date range, with target zones and slot assignments.</summary>
    [HttpGet("{guildId}/branches/{guildBranchId:int}/raids/board")]
    public async Task<IActionResult> GetBoard(
        string guildId,
        int guildBranchId,
        [FromQuery] DateOnly rangeStart,
        [FromQuery] DateOnly rangeEnd,
        CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetRaidBoardQuery, RaidBoardResponse>(
            new GetRaidBoardQuery { GuildId = guildId, GuildBranchId = guildBranchId, RequesterDiscordId = discordId, RangeStart = rangeStart, RangeEnd = rangeEnd },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>Returns a single raid event, with target zones and slot assignments — backs the raid detail page.</summary>
    [HttpGet("{guildId}/branches/{guildBranchId:int}/raids/events/{eventId:int}")]
    public async Task<IActionResult> GetEvent(string guildId, int guildBranchId, int eventId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetRaidEventQuery, RaidEventResponse>(
            new GetRaidEventQuery { GuildId = guildId, GuildBranchId = guildBranchId, EventId = eventId, RequesterDiscordId = discordId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>Officer-only — lists the branch's raid events (draft and published) within the lockout window around the given date, for the "extends the lockout of" picker in the create/edit dialogs.</summary>
    [HttpGet("{guildId}/branches/{guildBranchId:int}/raids/events/choices")]
    public async Task<IActionResult> GetEventChoices(string guildId, int guildBranchId, [FromQuery] DateTime aroundStartsAtUtc, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetRaidEventChoicesForBranchQuery, List<RaidEventChoiceResponse>>(
            new GetRaidEventChoicesForBranchQuery { GuildId = guildId, GuildBranchId = guildBranchId, RequesterDiscordId = discordId, AroundStartsAtUtc = aroundStartsAtUtc },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>Creates a standalone raid event, not tied to any recurring series.</summary>
    [HttpPost("{guildId}/branches/{guildBranchId:int}/raids/events")]
    public async Task<IActionResult> CreateEvent(string guildId, int guildBranchId, [FromBody] CreateAdhocRaidEventCommand command, CancellationToken cancellationToken)
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

    /// <summary>Updates a raid event's schedule and target-zone set.</summary>
    [HttpPatch("{guildId}/branches/{guildBranchId:int}/raids/events/{eventId:int}")]
    public async Task<IActionResult> UpdateEvent(string guildId, int guildBranchId, int eventId, [FromBody] UpdateRaidEventCommand command, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.GuildId = guildId;
        command.GuildBranchId = guildBranchId;
        command.RequesterDiscordId = discordId;
        command.EventId = eventId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Permanently deletes a raid event, including any slot assignments it has.</summary>
    [HttpDelete("{guildId}/branches/{guildBranchId:int}/raids/events/{eventId:int}")]
    public async Task<IActionResult> DeleteEvent(string guildId, int guildBranchId, int eventId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await CommandDispatcher.DispatchAsync(
            new DeleteRaidEventCommand { GuildId = guildId, GuildBranchId = guildBranchId, RequesterDiscordId = discordId, EventId = eventId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>Publishes a raid event, making it visible to non-officer roster members.</summary>
    [HttpPost("{guildId}/branches/{guildBranchId:int}/raids/events/{eventId:int}/publish")]
    public async Task<IActionResult> PublishEvent(string guildId, int guildBranchId, int eventId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await CommandDispatcher.DispatchAsync(
            new PublishRaidEventCommand { GuildId = guildId, GuildBranchId = guildBranchId, RequesterDiscordId = discordId, EventId = eventId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>Returns the characters currently assigned to a raid event — backs the "who should players whisper?" picker.</summary>
    [HttpGet("{guildId}/branches/{guildBranchId:int}/raids/events/{eventId:int}/assigned-characters")]
    public async Task<IActionResult> GetAssignedCharacters(string guildId, int guildBranchId, int eventId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetRaidEventAssignedCharactersQuery, List<RaidEventAssignedCharacterResponse>>(
            new GetRaidEventAssignedCharactersQuery { GuildId = guildId, GuildBranchId = guildBranchId, EventId = eventId, RequesterDiscordId = discordId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>Posts a one-off "grouping up now" ping, referencing the requester's (or an explicitly named) assigned character, in the branch's composition-announcement channel.</summary>
    [HttpPost("{guildId}/branches/{guildBranchId:int}/raids/events/{eventId:int}/announce-grouping")]
    public async Task<IActionResult> AnnounceGrouping(string guildId, int guildBranchId, int eventId, [FromBody] TriggerRaidGroupingCommand command, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.GuildId = guildId;
        command.GuildBranchId = guildBranchId;
        command.RequesterDiscordId = discordId;
        command.EventId = eventId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Assigns a character to a (group, slot) coordinate of a raid event's grid.</summary>
    [HttpPost("{guildId}/branches/{guildBranchId:int}/raids/events/{eventId:int}/slots/assign")]
    public async Task<IActionResult> AssignSlot(string guildId, int guildBranchId, int eventId, [FromBody] AssignCharacterToSlotCommand command, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.GuildId = guildId;
        command.GuildBranchId = guildBranchId;
        command.RequesterDiscordId = discordId;
        command.EventId = eventId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Swaps the characters assigned at two (group, slot) coordinates of the same raid event's grid.</summary>
    [HttpPost("{guildId}/branches/{guildBranchId:int}/raids/events/{eventId:int}/slots/swap")]
    public async Task<IActionResult> SwapSlotAssignments(string guildId, int guildBranchId, int eventId, [FromBody] SwapSlotAssignmentsCommand command, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.GuildId = guildId;
        command.GuildBranchId = guildBranchId;
        command.RequesterDiscordId = discordId;
        command.EventId = eventId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Clears a (group, slot) coordinate of a raid event's grid.</summary>
    [HttpPost("{guildId}/branches/{guildBranchId:int}/raids/events/{eventId:int}/slots/unassign")]
    public async Task<IActionResult> UnassignSlot(string guildId, int guildBranchId, int eventId, [FromBody] UnassignSlotCommand command, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.GuildId = guildId;
        command.GuildBranchId = guildBranchId;
        command.RequesterDiscordId = discordId;
        command.EventId = eventId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Changes which of a character's declared raid specs a slot assignment counts as.</summary>
    [HttpPatch("{guildId}/branches/{guildBranchId:int}/raids/events/{eventId:int}/slots/spec")]
    public async Task<IActionResult> UpdateSlotAssignmentSpec(string guildId, int guildBranchId, int eventId, [FromBody] UpdateSlotAssignmentSpecCommand command, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.GuildId = guildId;
        command.GuildBranchId = guildBranchId;
        command.RequesterDiscordId = discordId;
        command.EventId = eventId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Officer-only — creates a new Discord text channel, for immediate use as a raid's dedicated announcement channel.</summary>
    [HttpPost("{guildId}/branches/{guildBranchId:int}/raids/announcement-channel")]
    public async Task<IActionResult> CreateAnnouncementChannel(string guildId, int guildBranchId, [FromBody] CreateRaidAnnouncementChannelCommand command, CancellationToken cancellationToken)
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

    /// <summary>Sets the requesting member's own Accepted/Tentative/Declined response to a Signup-mode raid event.</summary>
    [HttpPost("{guildId}/branches/{guildBranchId:int}/raids/events/{eventId:int}/signup")]
    public async Task<IActionResult> SetMySignup(string guildId, int guildBranchId, int eventId, [FromBody] SetMyRaidSignupCommand command, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.GuildId = guildId;
        command.GuildBranchId = guildBranchId;
        command.RequesterDiscordId = discordId;
        command.EventId = eventId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Returns every roster member's current response to a Signup-mode raid event.</summary>
    [HttpGet("{guildId}/branches/{guildBranchId:int}/raids/events/{eventId:int}/signups")]
    public async Task<IActionResult> GetSignups(string guildId, int guildBranchId, int eventId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetRaidSignupsQuery, List<RaidSignupResponse>>(
            new GetRaidSignupsQuery { GuildId = guildId, GuildBranchId = guildBranchId, EventId = eventId, RequesterDiscordId = discordId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>Returns every active roster character not assigned to any raid event within a date range.</summary>
    [HttpGet("{guildId}/branches/{guildBranchId:int}/raids/unassigned-members")]
    public async Task<IActionResult> GetUnassignedMembers(
        string guildId,
        int guildBranchId,
        [FromQuery] DateOnly rangeStart,
        [FromQuery] DateOnly rangeEnd,
        CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetUnassignedGuildMembersQuery, List<UnassignedMemberResponse>>(
            new GetUnassignedGuildMembersQuery { GuildId = guildId, GuildBranchId = guildBranchId, RequesterDiscordId = discordId, RangeStart = rangeStart, RangeEnd = rangeEnd },
            cancellationToken);

        return ToActionResult(result);
    }
}
