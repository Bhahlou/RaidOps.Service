using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Raids.Spells.Commands;

/// <summary>
/// Polls wago.tools for the latest build of every active, wago-tracked <c>Branch</c>, and for any
/// branch whose build changed (or every branch, if <see cref="Force"/>), pulls the current
/// <c>SpellName</c>/<c>SpellMisc</c> DB2 exports and upserts the <c>Spell</c> reference table.
/// Dispatched hourly by <c>SpellSyncBackgroundService</c> (<see cref="Force"/> = false) and on
/// demand by the admin-only manual trigger (<see cref="Force"/> = true).
/// </summary>
public class SyncSpellsCommand : ICommandRequest
{
    /// <summary>
    /// When true, re-syncs every eligible branch against its current build even if already synced
    /// up to it — used by the manual trigger to re-run after fixing a transform bug, without waiting
    /// for a new build. When false, a branch already synced to the latest build is skipped.
    /// </summary>
    public bool Force { get; set; }
}
