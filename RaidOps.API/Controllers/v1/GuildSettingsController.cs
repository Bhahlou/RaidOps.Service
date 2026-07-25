using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Settings.Commands;
using RaidOps.Application.Contracts.Guilds.Settings.Queries;
using RaidOps.Application.Contracts.Guilds.Settings.Responses;
using System.IdentityModel.Tokens.Jwt;

namespace RaidOps.API.Controllers.v1;

/// <summary>
/// Exposes guild settings endpoints: read and write settings, fetch Discord roles,
/// read and write the Officer role threshold, read and write Discord notification settings.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/guilds")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class GuildSettingsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ApiControllerBase(commandDispatcher, queryDispatcher)
{
    /// <summary>
    /// Returns the current settings (timezone, roster mode, role threshold) of the specified guild.
    /// </summary>
    [HttpGet("{guildId}/settings")]
    public async Task<IActionResult> GetSettings(string guildId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetGuildSettingsQuery, GuildSettingsResponse>(
            new GetGuildSettingsQuery { GuildId = guildId, RequesterDiscordId = discordId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>
    /// Returns the assignable Discord roles for the specified guild.
    /// </summary>
    [HttpGet("{guildId}/discord-roles")]
    public async Task<IActionResult> GetDiscordRoles(string guildId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetGuildDiscordRolesQuery, List<DiscordRoleResponse>>(
            new GetGuildDiscordRolesQuery { GuildId = guildId, RequesterDiscordId = discordId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>
    /// Persists the guild settings (timezone, roster mode and role threshold).
    /// </summary>
    [HttpPatch("{guildId}/settings")]
    public async Task<IActionResult> UpdateSettings(
        string guildId,
        [FromBody] UpdateGuildSettingsCommand command,
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
    /// Returns the current Officer role threshold of the specified guild.
    /// </summary>
    [HttpGet("{guildId}/officer-threshold")]
    public async Task<IActionResult> GetOfficerThreshold(string guildId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetOfficerThresholdQuery, OfficerThresholdResponse>(
            new GetOfficerThresholdQuery { GuildId = guildId, RequesterDiscordId = discordId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>
    /// Persists the Officer role threshold for the specified guild, independently of the rest
    /// of guild settings.
    /// </summary>
    [HttpPatch("{guildId}/officer-threshold")]
    public async Task<IActionResult> UpdateOfficerThreshold(
        string guildId,
        [FromBody] UpdateOfficerThresholdCommand command,
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
    /// Returns the guild's Discord notification settings (one entry per event type).
    /// </summary>
    [HttpGet("{guildId}/notification-settings")]
    public async Task<IActionResult> GetNotificationSettings(string guildId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetGuildNotificationSettingsQuery, List<GuildNotificationSettingResponse>>(
            new GetGuildNotificationSettingsQuery { GuildId = guildId, RequesterDiscordId = discordId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>
    /// Returns the guild's text-postable Discord channels, annotated with whether the bot can
    /// currently post in each, for the notification settings channel picker.
    /// </summary>
    [HttpGet("{guildId}/notification-channels")]
    public async Task<IActionResult> GetNotificationChannels(string guildId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetGuildNotificationChannelsQuery, List<DiscordChannelResponse>>(
            new GetGuildNotificationChannelsQuery { GuildId = guildId, RequesterDiscordId = discordId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>
    /// Persists the guild's Discord notification settings in bulk.
    /// </summary>
    [HttpPatch("{guildId}/notification-settings")]
    public async Task<IActionResult> UpdateNotificationSettings(
        string guildId,
        [FromBody] UpdateGuildNotificationSettingsCommand command,
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
}
