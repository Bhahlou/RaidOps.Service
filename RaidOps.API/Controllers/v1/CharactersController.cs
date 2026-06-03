using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaidOps.API.Requests;
using RaidOps.Application.Contracts.Characters.Commands;
using RaidOps.Application.Contracts.Characters.Queries;
using RaidOps.Application.Contracts.Characters.Responses;
using RaidOps.Application.Contracts.CQRS;
using System.IdentityModel.Tokens.Jwt;

namespace RaidOps.API.Controllers.v1;

/// <summary>
/// Exposes character management endpoints.
/// All routes require a valid JWT Bearer token.
/// </summary>
[ApiVersion("1.0")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class CharactersController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ApiControllerBase(commandDispatcher, queryDispatcher)
{
    /// <summary>
    /// Returns all WoW characters the user has activated in RaidOps.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null) return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetCharactersQuery, IEnumerable<CharacterDto>>(
            new GetCharactersQuery { UserDiscordId = discordId }, cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>
    /// Returns all WoW characters synced from BNet for the user,
    /// including those not yet activated in RaidOps.
    /// Used to populate the character selection dialog.
    /// </summary>
    [HttpGet("synced")]
    public async Task<IActionResult> GetSynced(CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null) return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetSyncedCharactersQuery, IEnumerable<SyncedCharacterDto>>(
            new GetSyncedCharactersQuery { UserDiscordId = discordId }, cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>
    /// Fetches all WoW characters from the user's BNet account for the given branch
    /// and upserts them into the database. Requires a fresh BNet token (handled by the client
    /// via the OAuth iframe flow before calling this endpoint).
    /// </summary>
    [HttpPost("sync")]
    public async Task<IActionResult> Sync(
        [FromBody] SyncBnetCharactersRequest request,
        CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null) return Unauthorized();

        var result = await CommandDispatcher.DispatchAsync(new SyncBnetCharactersCommand
        {
            UserDiscordId = discordId,
            BranchId = request.BranchId
        }, cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>
    /// Marks the given characters as active in RaidOps.
    /// Characters must already be synced and belong to the authenticated user.
    /// </summary>
    [HttpPost("activate")]
    public async Task<IActionResult> Activate(
        [FromBody] ActivateCharactersRequest request,
        CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null) return Unauthorized();

        var result = await CommandDispatcher.DispatchAsync(new ActivateCharactersCommand
        {
            UserDiscordId = discordId,
            CharacterIds = request.CharacterIds
        }, cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>
    /// Re-fetches the character's data from Battle.net and returns the updated character.
    /// The character must be active and belong to the authenticated user.
    /// </summary>
    [HttpPost("{id:int}/resync")]
    public async Task<IActionResult> Resync(int id, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null) return Unauthorized();

        var result = await CommandDispatcher.DispatchAsync(new ResyncCharacterCommand
        {
            UserDiscordId = discordId,
            CharacterId = id
        }, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, detail = result.Detail });

        return Ok(result.Value!.Body);
    }

    /// <summary>
    /// Sets <c>IsActiveInRaidOps = false</c> for the given character.
    /// The character must belong to the authenticated user.
    /// </summary>
    [HttpPost("{id:int}/deactivate")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null) return Unauthorized();

        var result = await CommandDispatcher.DispatchAsync(new DeactivateCharacterCommand
        {
            UserDiscordId = discordId,
            CharacterId = id
        }, cancellationToken);

        return ToActionResult(result);
    }
}
