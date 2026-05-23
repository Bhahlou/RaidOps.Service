using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaidOps.Application.Contracts.Branches.Queries;
using RaidOps.Application.Contracts.Branches.Responses;
using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.API.Controllers.v1;

/// <summary>
/// Exposes read-only access to the WoW branch (game version) reference table.
/// Used by the character import dialog to populate the branch picker.
/// </summary>
[ApiVersion("1.0")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class BranchesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ApiControllerBase(commandDispatcher, queryDispatcher)
{
    /// <summary>
    /// Returns all available WoW branches ordered by ID.
    /// </summary>
    /// <returns>200 with a list of <see cref="BranchDto"/>.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await QueryDispatcher.DispatchAsync<GetBranchesQuery, IEnumerable<BranchDto>>(
            new GetBranchesQuery(), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }
}
