using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaidOps.Application.Contracts.Calendar.Availability.Commands;
using RaidOps.Application.Contracts.Calendar.Availability.Queries;
using RaidOps.Application.Contracts.Calendar.Availability.Responses;
using RaidOps.Application.Contracts.CQRS;
using System.IdentityModel.Tokens.Jwt;

namespace RaidOps.API.Controllers.v1;

/// <summary>
/// Manages a member's own availability declarations (one-off exceptions and recurring patterns)
/// for a specific guild.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/guilds")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class AvailabilityController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ApiControllerBase(commandDispatcher, queryDispatcher)
{
    /// <summary>
    /// Returns the requesting member's resolved availability calendar over a date range,
    /// along with the raw exceptions and recurring patterns backing it.
    /// </summary>
    [HttpGet("{guildId}/availability")]
    public async Task<IActionResult> GetMyAvailability(
        string guildId,
        [FromQuery] DateOnly rangeStart,
        [FromQuery] DateOnly rangeEnd,
        CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetMyAvailabilityQuery, AvailabilityCalendarResponse>(
            new GetMyAvailabilityQuery { GuildId = guildId, RequesterDiscordId = discordId, RangeStart = rangeStart, RangeEnd = rangeEnd },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>
    /// Declares a one-off availability exception for a single date or date range.
    /// </summary>
    [HttpPost("{guildId}/availability/exceptions")]
    public async Task<IActionResult> CreateException(
        string guildId,
        [FromBody] CreateAvailabilityExceptionCommand command,
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
    /// Deletes one of the requesting member's own one-off availability exceptions.
    /// </summary>
    [HttpDelete("{guildId}/availability/exceptions/{exceptionId:int}")]
    public async Task<IActionResult> DeleteException(string guildId, int exceptionId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await CommandDispatcher.DispatchAsync(
            new DeleteAvailabilityExceptionCommand { GuildId = guildId, RequesterDiscordId = discordId, ExceptionId = exceptionId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>
    /// Replaces the dates/status of one of the requesting member's own one-off availability
    /// exceptions.
    /// </summary>
    [HttpPatch("{guildId}/availability/exceptions/{exceptionId:int}")]
    public async Task<IActionResult> UpdateException(
        string guildId,
        int exceptionId,
        [FromBody] UpdateAvailabilityExceptionCommand command,
        CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.GuildId = guildId;
        command.RequesterDiscordId = discordId;
        command.ExceptionId = exceptionId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Clears a single day out of one of the requesting member's own one-off availability
    /// exceptions, shrinking or splitting it as needed.
    /// </summary>
    [HttpPost("{guildId}/availability/exceptions/{exceptionId:int}/remove-day")]
    public async Task<IActionResult> RemoveExceptionDay(
        string guildId,
        int exceptionId,
        [FromBody] RemoveAvailabilityExceptionDayCommand command,
        CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.GuildId = guildId;
        command.RequesterDiscordId = discordId;
        command.ExceptionId = exceptionId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Creates a recurring availability pattern (e.g. a weekly recurrence, or a shift rotation).
    /// </summary>
    [HttpPost("{guildId}/availability/patterns")]
    public async Task<IActionResult> CreatePattern(
        string guildId,
        [FromBody] CreateRecurringAvailabilityPatternCommand command,
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
    /// Replaces the settings and full day set of one of the requesting member's own recurring patterns.
    /// </summary>
    [HttpPatch("{guildId}/availability/patterns/{patternId:int}")]
    public async Task<IActionResult> UpdatePattern(
        string guildId,
        int patternId,
        [FromBody] UpdateRecurringAvailabilityPatternCommand command,
        CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.GuildId = guildId;
        command.RequesterDiscordId = discordId;
        command.PatternId = patternId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Deletes one of the requesting member's own recurring availability patterns.
    /// </summary>
    [HttpDelete("{guildId}/availability/patterns/{patternId:int}")]
    public async Task<IActionResult> DeletePattern(string guildId, int patternId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await CommandDispatcher.DispatchAsync(
            new DeleteRecurringAvailabilityPatternCommand { GuildId = guildId, RequesterDiscordId = discordId, PatternId = patternId },
            cancellationToken);

        return ToActionResult(result);
    }
}
