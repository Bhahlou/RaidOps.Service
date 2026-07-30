using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Settings.Commands;
using RaidOps.Application.Contracts.Guilds.Settings.Queries;
using RaidOps.Application.Contracts.Guilds.Settings.Responses;
using RaidOps.Domain.Enums;
using System.IdentityModel.Tokens.Jwt;

namespace RaidOps.API.Controllers.v1;

/// <summary>
/// Exposes guild-level settings endpoints: read and write identity settings (timezone, language),
/// fetch Discord roles, read and write Discord notification settings. Branch-scoped settings
/// (roster mode, roster/officer role sets) live under <see cref="GuildBranchesController"/>.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/guilds")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class GuildSettingsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ApiControllerBase(commandDispatcher, queryDispatcher)
{
    /// <summary>
    /// Returns the current guild-level identity settings (timezone, language) of the specified guild.
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
    /// Persists the guild-level identity settings (timezone and language).
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
    /// Returns the guild's Discord notification settings (one entry per event type).
    /// </summary>
    [HttpGet("{guildId}/notification-settings")]
    public async Task<IActionResult> GetNotificationSettings(string guildId, [FromQuery] int? guildBranchId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetGuildNotificationSettingsQuery, List<GuildNotificationSettingResponse>>(
            new GetGuildNotificationSettingsQuery { GuildId = guildId, RequesterDiscordId = discordId, GuildBranchId = guildBranchId },
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

    /// <summary>
    /// Removes the branch's notification-settings override for one event type, reverting just
    /// that setting to inheriting the guild-wide fallback.
    /// </summary>
    [HttpDelete("{guildId}/notification-settings/{guildBranchId:int}/{eventType}")]
    public async Task<IActionResult> ResetNotificationSetting(
        string guildId,
        int guildBranchId,
        GuildNotificationEventType eventType,
        CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await CommandDispatcher.DispatchAsync(
            new ResetGuildNotificationSettingsCommand { GuildId = guildId, GuildBranchId = guildBranchId, EventType = eventType, RequesterDiscordId = discordId },
            cancellationToken);

        return ToActionResult(result);
    }
}
