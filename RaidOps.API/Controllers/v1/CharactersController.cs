using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaidOps.Application.Contracts.Characters.Commands;
using RaidOps.Application.Contracts.Characters.Queries;
using RaidOps.Application.Contracts.Characters.Responses;
using RaidOps.Application.Contracts.CQRS;
using System.IdentityModel.Tokens.Jwt;

namespace RaidOps.API.Controllers.v1;

/// <summary>
/// Exposes character management endpoints:
/// listing characters available for import from BNet, and triggering the import.
/// All routes require a valid JWT Bearer token.
/// </summary>
[ApiVersion("1.0")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class CharactersController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ApiControllerBase(commandDispatcher, queryDispatcher)
{
    /// <summary>
    /// Returns all WoW characters imported by the authenticated user.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>200 with a list of <see cref="CharacterDto"/>, or 401 if the JWT is invalid.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null) return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetCharactersQuery, IEnumerable<CharacterDto>>(
            new GetCharactersQuery { UserDiscordId = discordId },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>
    /// Returns the list of WoW characters available for import from the user's BNet account
    /// for the specified branch.
    /// Each entry includes an <c>alreadyImported</c> flag indicating whether the character
    /// has already been imported into RaidOps.
    /// </summary>
    /// <param name="branchId">ID of the branch to query characters for.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// 200 with a list of <see cref="AvailableCharacterDto"/>,
    /// 400 if the branch is not found or the BNet account is not linked,
    /// or 401 if the JWT is invalid.
    /// </returns>
    [HttpGet("available")]
    public async Task<IActionResult> GetAvailable(
        [FromQuery] int branchId,
        CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null) return Unauthorized();

        var result = await QueryDispatcher.DispatchAsync<GetAvailableCharactersQuery, IEnumerable<AvailableCharacterDto>>(
            new GetAvailableCharactersQuery
            {
                UserDiscordId = discordId,
                BranchId = branchId
            }, cancellationToken);

        if (result.IsFailed)
        {
            return result.Error switch
            {
                "BNET_NOT_LINKED" => BadRequest(new { error = "BNET_NOT_LINKED" }),
                "BRANCH_NOT_FOUND" => NotFound(new { error = "BRANCH_NOT_FOUND" }),
                _ => BadRequest(new { error = result.Error })
            };
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Imports the selected WoW characters from the user's BNet account into RaidOps.
    /// Upserts characters and their expansion states; realms are cached on-demand.
    /// </summary>
    /// <param name="request">The list of characters to import along with the target branch.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// 200 with a success message, 400 if validation fails, or 401 if the JWT is invalid.
    /// </returns>
    [HttpPost("import")]
    public async Task<IActionResult> Import(
        [FromBody] ImportCharactersRequest request,
        CancellationToken cancellationToken)
    {
        var discordId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null) return Unauthorized();

        var result = await CommandDispatcher.DispatchAsync(new ImportCharactersCommand
        {
            UserDiscordId = discordId,
            BranchId = request.BranchId,
            Characters = request.Characters
        }, cancellationToken);

        return ToActionResult(result);
    }
}

/// <summary>
/// Request body for <c>POST /api/v1/characters/import</c>.
/// </summary>
public class ImportCharactersRequest
{
    /// <summary>ID of the branch the characters are imported from.</summary>
    public required int BranchId { get; set; }

    /// <summary>Characters to import.</summary>
    public required IEnumerable<CharacterToImportDto> Characters { get; set; }
}
