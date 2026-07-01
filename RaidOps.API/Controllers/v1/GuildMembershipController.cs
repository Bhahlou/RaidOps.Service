using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Memberships.Commands;
using RaidOps.Application.Contracts.Guilds.Memberships.Queries;
using RaidOps.Application.Contracts.Guilds.Memberships.Responses;
using System.IdentityModel.Tokens.Jwt;

namespace RaidOps.API.Controllers.v1;

/// <summary>
/// Manages character-to-guild roster memberships.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class GuildMembershipController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ApiControllerBase(commandDispatcher, queryDispatcher)
{
    /// <summary>
    /// Returns all registered guilds the user can add at least one of their characters to,
    /// along with the specific eligible characters for each guild.
    /// Used by the get-started stepper for a guild-centric link UI (single request).
    /// </summary>
    [HttpGet("characters/eligible-guilds")]
    public async Task<IActionResult> GetEligibleGuildsBulk(CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null) return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetEligibleGuildsBulkQuery, List<GuildEligibilityResponse>>(
            new GetEligibleGuildsBulkQuery { RequesterDiscordId = discordId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>
    /// Returns the guilds that a character is eligible to join
    /// (Discord member, configured roster mode grants access, not yet a member).
    /// </summary>
    [HttpGet("characters/{characterId:int}/eligible-guilds")]
    public async Task<IActionResult> GetEligibleGuilds(int characterId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null) return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetEligibleGuildsQuery, List<EligibleGuildResponse>>(
            new GetEligibleGuildsQuery { CharacterId = characterId, RequesterDiscordId = discordId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>
    /// Adds a character to a guild's roster.
    /// </summary>
    [HttpPost("characters/{characterId:int}/memberships/{guildId}")]
    public async Task<IActionResult> JoinGuild(
        int characterId,
        string guildId,
        [FromBody] JoinGuildCommand command,
        CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null) return Unauthorized();

        command.CharacterId = characterId;
        command.GuildId = guildId;
        command.RequesterDiscordId = discordId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Updates a character's raid-composition rank on a guild roster.
    /// </summary>
    [HttpPatch("characters/{characterId:int}/memberships/{guildId}")]
    public async Task<IActionResult> UpdateCharacterRank(
        int characterId,
        string guildId,
        [FromBody] UpdateCharacterRankCommand command,
        CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null) return Unauthorized();

        command.CharacterId = characterId;
        command.GuildId = guildId;
        command.RequesterDiscordId = discordId;

        var result = await CommandDispatcher.DispatchAsync(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Removes a character from a guild's roster.
    /// </summary>
    [HttpDelete("characters/{characterId:int}/memberships/{guildId}")]
    public async Task<IActionResult> LeaveGuild(int characterId, string guildId, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null) return Unauthorized();

        var result = await CommandDispatcher.DispatchAsync(
            new LeaveGuildCommand { CharacterId = characterId, GuildId = guildId, RequesterDiscordId = discordId },
            cancellationToken);

        return ToActionResult(result);
    }
}
