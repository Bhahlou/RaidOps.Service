using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Attributions.Commands;
using RaidOps.Application.Contracts.Raids.Attributions.Queries;
using RaidOps.Application.Contracts.Raids.Attributions.Responses;
using RaidOps.Application.Contracts.Raids.Bosses.Queries;
using RaidOps.Application.Contracts.Raids.Bosses.Responses;
using RaidOps.Application.Contracts.Raids.Spells.Queries;
using RaidOps.Application.Contracts.Raids.Spells.Responses;
using RaidOps.Application.Contracts.Raids.Zones.Queries;
using RaidOps.Application.Contracts.Raids.Zones.Responses;
using System.IdentityModel.Tokens.Jwt;

namespace RaidOps.API.Controllers.v1;

/// <summary>
/// Manages a guild's raid-attribution template (buffs, curses, tank/heal swaps, …) and the spell
/// search backing its icon picker. Officer-only — see each handler's access check.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/guilds")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class GuildAttributionDefinitionsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ApiControllerBase(commandDispatcher, queryDispatcher)
{
    /// <summary>Returns the guild's raid-attribution template for one scope ("General", or one specific boss), ordered for display.</summary>
    [HttpGet("{guildId}/attribution-definitions")]
    public async Task<IActionResult> GetDefinitions(string guildId, [FromQuery] int? raidBossId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetGuildAttributionDefinitionsQuery, List<GuildAttributionDefinitionResponse>>(
            new GetGuildAttributionDefinitionsQuery { GuildId = guildId, RequesterDiscordId = discordId, RaidBossId = raidBossId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>Returns the union of raid zones available across every active branch of the guild — backs the template editor's "raid" scope picker.</summary>
    [HttpGet("{guildId}/raid-zones")]
    public async Task<IActionResult> GetRaidZonesForGuild(string guildId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetRaidZonesForGuildQuery, List<RaidZoneResponse>>(
            new GetRaidZonesForGuildQuery { GuildId = guildId, RequesterDiscordId = discordId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>Returns every boss of one raid zone — backs the template editor's boss picker once a raid is chosen.</summary>
    [HttpGet("{guildId}/raid-zones/{raidZoneId:int}/bosses")]
    public async Task<IActionResult> GetBossesForZone(string guildId, int raidZoneId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetRaidBossesForZoneQuery, List<RaidBossResponse>>(
            new GetRaidBossesForZoneQuery { GuildId = guildId, RequesterDiscordId = discordId, RaidZoneId = raidZoneId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>Adds a new row to the guild's raid-attribution template.</summary>
    [HttpPost("{guildId}/attribution-definitions")]
    public async Task<IActionResult> CreateDefinition(string guildId, [FromBody] CreateGuildAttributionDefinitionCommand command, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.GuildId = guildId;
        command.RequesterDiscordId = discordId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Updates an existing row of the guild's raid-attribution template.</summary>
    [HttpPatch("{guildId}/attribution-definitions/{definitionId:int}")]
    public async Task<IActionResult> UpdateDefinition(string guildId, int definitionId, [FromBody] UpdateGuildAttributionDefinitionCommand command, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.GuildId = guildId;
        command.RequesterDiscordId = discordId;
        command.DefinitionId = definitionId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Permanently deletes a row of the guild's raid-attribution template.</summary>
    [HttpDelete("{guildId}/attribution-definitions/{definitionId:int}")]
    public async Task<IActionResult> DeleteDefinition(string guildId, int definitionId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await CommandDispatcher.DispatchAsync(
            new DeleteGuildAttributionDefinitionCommand { GuildId = guildId, RequesterDiscordId = discordId, DefinitionId = definitionId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>Re-numbers the guild's template rows to match the given order.</summary>
    [HttpPost("{guildId}/attribution-definitions/reorder")]
    public async Task<IActionResult> ReorderDefinitions(string guildId, [FromBody] ReorderGuildAttributionDefinitionsCommand command, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.GuildId = guildId;
        command.RequesterDiscordId = discordId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Sets the section-header icon shown above every row sharing one (scope, section) tuple.</summary>
    [HttpPost("{guildId}/attribution-definitions/sections/icon")]
    public async Task<IActionResult> SetSectionIcon(string guildId, [FromBody] SetAttributionSectionIconCommand command, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        command.GuildId = guildId;
        command.RequesterDiscordId = discordId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Searches the seeded spell reference table by localized name substring — backs the template editor's spell picker.</summary>
    [HttpGet("{guildId}/spells/search")]
    public async Task<IActionResult> SearchSpells(string guildId, [FromQuery] int expansionId, [FromQuery] string searchTerm, [FromQuery] string locale, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<SearchSpellsQuery, List<SpellResponse>>(
            new SearchSpellsQuery { GuildId = guildId, RequesterDiscordId = discordId, ExpansionId = expansionId, SearchTerm = searchTerm, Locale = locale },
            cancellationToken);

        return ToActionResult(result);
    }
}
