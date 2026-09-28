using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaidOps.API.Authorization;
using RaidOps.API.Requests;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Buffs;
using RaidOps.Application.Contracts.Raids.Buffs.Commands;
using RaidOps.Application.Contracts.Raids.Spells.Commands;

namespace RaidOps.API.Controllers.v1;

/// <summary>
/// Owner-only operational endpoints — gated by <see cref="OwnerOnlyAttribute"/> (the requester's own
/// Discord ID listed in <c>Admin:OwnerDiscordIds</c>) rather than a guild-scoped permission, since these
/// actions aren't tied to any one guild. Still goes through the normal JWT cookie auth like every other
/// controller; there's no separate admin login.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[OwnerOnly]
public class AdminController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ApiControllerBase(commandDispatcher, queryDispatcher)
{
    // A full, real definitions list is a few tens of KB; this only exists to refuse absurd payloads early.
    private const int MaxRaidBuffBodyBytes = 1_048_576;

    /// <summary>
    /// Re-syncs the <c>Spell</c> reference table from wago.tools right now, for every active
    /// wago-tracked branch — even branches already synced to their current build (<see cref="SyncSpellsCommand.Force"/>),
    /// so this is also useful to re-run after fixing a transform bug without waiting for a new build.
    /// </summary>
    [HttpPost("sync-spells")]
    public async Task<IActionResult> SyncSpells(CancellationToken cancellationToken)
    {
        var result = await CommandDispatcher.DispatchAsync(new SyncSpellsCommand { Force = true }, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Creates or overwrites one raid buff/debuff definition of an expansion, matched by its spell. The
    /// response body is a <c>RaidBuffUpsertSummary</c>.
    /// </summary>
    [HttpPut("raid-buffs/{expansionId:int}")]
    [RequestSizeLimit(MaxRaidBuffBodyBytes)]
    public async Task<IActionResult> SaveRaidBuff(int expansionId, [FromBody] RaidBuffDefinitionInput definition, CancellationToken cancellationToken)
    {
        var result = await CommandDispatcher.DispatchAsync(
            new UpsertRaidBuffDefinitionsCommand { ExpansionId = expansionId, Definitions = [definition] },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>
    /// Writes a whole exported definitions file to an expansion, all-or-nothing, optionally deleting the
    /// definitions the file doesn't contain so the expansion matches it exactly. Used to align
    /// environments without redeploying. The response body is a <c>RaidBuffUpsertSummary</c>.
    /// </summary>
    [HttpPost("raid-buffs/{expansionId:int}/import")]
    [RequestSizeLimit(MaxRaidBuffBodyBytes)]
    public async Task<IActionResult> ImportRaidBuffs(int expansionId, [FromBody] ImportRaidBuffDefinitionsRequest request, CancellationToken cancellationToken)
    {
        var result = await CommandDispatcher.DispatchAsync(
            new UpsertRaidBuffDefinitionsCommand { ExpansionId = expansionId, Definitions = request.Definitions, PruneMissing = request.PruneMissing ?? false },
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>Edits one existing raid buff/debuff definition by its surrogate ID — including changing its spell.</summary>
    [HttpPut("raid-buffs/definitions/{id:int}")]
    [RequestSizeLimit(MaxRaidBuffBodyBytes)]
    public async Task<IActionResult> UpdateRaidBuff(int id, [FromBody] RaidBuffDefinitionInput definition, CancellationToken cancellationToken)
    {
        var result = await CommandDispatcher.DispatchAsync(new UpdateRaidBuffDefinitionCommand { Id = id, Definition = definition }, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Deletes one raid buff/debuff definition by its surrogate ID.</summary>
    [HttpDelete("raid-buffs/{id:int}")]
    public async Task<IActionResult> DeleteRaidBuff(int id, CancellationToken cancellationToken)
    {
        var result = await CommandDispatcher.DispatchAsync(new DeleteRaidBuffDefinitionCommand { Id = id }, cancellationToken);
        return ToActionResult(result);
    }
}
