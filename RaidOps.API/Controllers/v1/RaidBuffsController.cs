using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Buffs.Queries;
using RaidOps.Application.Contracts.Raids.Buffs.Responses;

namespace RaidOps.API.Controllers.v1;

/// <summary>
/// Read-only access to the curated raid buff/debuff definitions, which are public reference data: any
/// authenticated user may read them. They are edited through the owner-only <c>AdminController</c>.
/// </summary>
[ApiVersion("1.0")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class RaidBuffsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ApiControllerBase(commandDispatcher, queryDispatcher)
{
    /// <summary>Returns the raid buff/debuff definitions of one expansion, ordered for display.</summary>
    /// <returns>200 with a list of <see cref="RaidBuffDefinitionResponse"/>.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int expansionId, CancellationToken cancellationToken)
    {
        var result = await QueryDispatcher.DispatchAsync<GetRaidBuffDefinitionsQuery, List<RaidBuffDefinitionResponse>>(
            new GetRaidBuffDefinitionsQuery { ExpansionId = expansionId }, cancellationToken);

        return ToActionResult(result);
    }
}
