using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Spells.Queries;
using RaidOps.Application.Contracts.Raids.Spells.Responses;

namespace RaidOps.API.Controllers.v1;

/// <summary>
/// Read-only access to the spell reference data, which is public: any authenticated user may search it.
/// Backs every spell picker (attribution template editor, raid buff admin screen).
/// </summary>
[ApiVersion("1.0")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class SpellsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ApiControllerBase(commandDispatcher, queryDispatcher)
{
    /// <summary>Searches the spells of one expansion by localized name substring, each already resolved to its name and icon on that expansion.</summary>
    /// <returns>200 with a list of <see cref="SpellResponse"/>.</returns>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] int expansionId, [FromQuery] string searchTerm, [FromQuery] string locale, CancellationToken cancellationToken)
    {
        var result = await QueryDispatcher.DispatchAsync<SearchSpellsQuery, List<SpellResponse>>(
            new SearchSpellsQuery { ExpansionId = expansionId, SearchTerm = searchTerm, Locale = locale },
            cancellationToken);

        return ToActionResult(result);
    }
}
