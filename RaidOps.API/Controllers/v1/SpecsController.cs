using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Specs.Queries;
using RaidOps.Application.Contracts.Specs.Responses;

namespace RaidOps.API.Controllers.v1;

/// <summary>
/// Exposes read-only access to the WoW spec reference table.
/// Used by the front end to render class-constrained spec pickers (e.g. raid-viable specs).
/// </summary>
[ApiVersion("1.0")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class SpecsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ApiControllerBase(commandDispatcher, queryDispatcher)
{
    /// <summary>
    /// Returns all available WoW specs ordered by Blizzard ID.
    /// </summary>
    /// <returns>200 with a list of <see cref="SpecDto"/>.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await QueryDispatcher.DispatchAsync<GetSpecsQuery, IEnumerable<SpecDto>>(
            new GetSpecsQuery(), cancellationToken);

        return ToActionResult(result);
    }
}
