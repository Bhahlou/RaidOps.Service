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
/// Manages a member's own availability declarations (one-off exceptions and recurring patterns),
/// each independently scoped Global or to a specific guild branch.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/me/availability")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class AvailabilityController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ApiControllerBase(commandDispatcher, queryDispatcher)
{
    /// <summary>
    /// Returns the requesting member's resolved availability overview over a date range, across
    /// every scope, along with the raw exceptions and recurring patterns backing it.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMyAvailability(
        [FromQuery] DateOnly rangeStart,
        [FromQuery] DateOnly rangeEnd,
        CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetMyAvailabilityQuery, AvailabilityCalendarResponse>(
            new GetMyAvailabilityQuery { RequesterDiscordId = discordId, RangeStart = rangeStart, RangeEnd = rangeEnd },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>
    /// Declares a one-off availability exception for a single date or date range, either Global or
    /// scoped to a specific branch (both <see cref="CreateAvailabilityExceptionCommand.GuildId"/> and
    /// <see cref="CreateAvailabilityExceptionCommand.GuildBranchId"/> set).
    /// </summary>
    [HttpPost("exceptions")]
    public async Task<IActionResult> CreateException(
        [FromBody] CreateAvailabilityExceptionCommand command,
        CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.RequesterDiscordId = discordId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Deletes one of the requesting member's own one-off availability exceptions.
    /// </summary>
    [HttpDelete("exceptions/{exceptionId:int}")]
    public async Task<IActionResult> DeleteException(int exceptionId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await CommandDispatcher.DispatchAsync(
            new DeleteAvailabilityExceptionCommand { RequesterDiscordId = discordId, ExceptionId = exceptionId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>
    /// Replaces the dates/status of one of the requesting member's own one-off availability
    /// exceptions. Scope (Global/branch) is immutable — not part of this request.
    /// </summary>
    [HttpPatch("exceptions/{exceptionId:int}")]
    public async Task<IActionResult> UpdateException(
        int exceptionId,
        [FromBody] UpdateAvailabilityExceptionCommand command,
        CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.RequesterDiscordId = discordId;
        command.ExceptionId = exceptionId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Clears a single day out of one of the requesting member's own one-off availability
    /// exceptions, shrinking or splitting it as needed.
    /// </summary>
    [HttpPost("exceptions/{exceptionId:int}/remove-day")]
    public async Task<IActionResult> RemoveExceptionDay(
        int exceptionId,
        [FromBody] RemoveAvailabilityExceptionDayCommand command,
        CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.RequesterDiscordId = discordId;
        command.ExceptionId = exceptionId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Creates a recurring availability pattern (e.g. a weekly recurrence, or a shift rotation),
    /// either Global or scoped to a specific branch.
    /// </summary>
    [HttpPost("patterns")]
    public async Task<IActionResult> CreatePattern(
        [FromBody] CreateRecurringAvailabilityPatternCommand command,
        CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.RequesterDiscordId = discordId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Replaces the settings and full day set of one of the requesting member's own recurring
    /// patterns. Scope (Global/branch) is immutable — not part of this request.
    /// </summary>
    [HttpPatch("patterns/{patternId:int}")]
    public async Task<IActionResult> UpdatePattern(
        int patternId,
        [FromBody] UpdateRecurringAvailabilityPatternCommand command,
        CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.RequesterDiscordId = discordId;
        command.PatternId = patternId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Deletes one of the requesting member's own recurring availability patterns.
    /// </summary>
    [HttpDelete("patterns/{patternId:int}")]
    public async Task<IActionResult> DeletePattern(int patternId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await CommandDispatcher.DispatchAsync(
            new DeleteRecurringAvailabilityPatternCommand { RequesterDiscordId = discordId, PatternId = patternId },
            cancellationToken);

        return ToActionResult(result);
    }
}
