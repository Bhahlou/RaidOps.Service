using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Reference.Queries;
using RaidOps.Application.Contracts.Reference.Responses;

namespace RaidOps.API.Controllers.v1;

/// <summary>
/// Exposes read-only access to the WoW class reference table.
/// Used by the front end to render expansion-filtered class pickers (e.g. attribution slot restrictions).
/// </summary>
[ApiVersion("1.0")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class WowClassesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ApiControllerBase(commandDispatcher, queryDispatcher)
{
    /// <summary>Returns all WoW classes ordered by Blizzard ID, optionally filtered to those actually available on a given expansion.</summary>
    /// <returns>200 with a list of <see cref="WowClassDto"/>.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? availableForExpansionId, CancellationToken cancellationToken)
    {
        var result = await QueryDispatcher.DispatchAsync<GetWowClassesQuery, IEnumerable<WowClassDto>>(
            new GetWowClassesQuery { AvailableForExpansionId = availableForExpansionId }, cancellationToken);

        return ToActionResult(result);
    }
}
