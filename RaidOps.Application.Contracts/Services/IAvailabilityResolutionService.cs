using RaidOps.Application.Contracts.Calendar.Availability.Responses;
using RaidOps.Domain.Models.Calendar;

namespace RaidOps.Application.Contracts.Services;

/// <summary>
/// Resolves a member's day-by-day availability over a date range from their one-off exceptions
/// and recurring patterns, which can each be scoped Global or to a specific <c>GuildBranch</c>.
/// Pure function of its inputs — no persistence, easily unit-testable.
/// </summary>
public interface IAvailabilityResolutionService
{
    /// <summary>
    /// Returns a personal, at-a-glance resolved status for every date in
    /// <paramref name="rangeStart"/>..<paramref name="rangeEnd"/>, considering every scope the
    /// member has declarations in. For a given date, each scope (Global, and every branch with at
    /// least one declaration) is resolved independently via <see cref="ResolveForScope"/>, and the
    /// single most restrictive result across all of them is returned (<c>Absent</c> &gt;
    /// <c>Partial</c> &gt; <c>Available</c>). This is never the authoritative status for any one
    /// guild/branch — it's a personal overview only, since two scopes can legitimately disagree on
    /// the same date (e.g. absent for branch A, available everywhere else).
    /// </summary>
    /// <param name="rangeStart">First date to resolve (inclusive).</param>
    /// <param name="rangeEnd">Last date to resolve (inclusive).</param>
    /// <param name="exceptions">The member's one-off exceptions across every scope, expected to overlap the range.</param>
    /// <param name="patterns">The member's recurring patterns across every scope (active or not — inactive ones are ignored).</param>
    List<ResolvedDayAvailabilityResponse> Resolve(
        DateOnly rangeStart,
        DateOnly rangeEnd,
        IReadOnlyCollection<AvailabilityDeclaration> exceptions,
        IReadOnlyCollection<RecurringAvailabilityPattern> patterns);

    /// <summary>
    /// Returns the resolved status for every date in <paramref name="rangeStart"/>..<paramref name="rangeEnd"/>,
    /// authoritative for the single scope identified by <paramref name="guildId"/>/<paramref name="guildBranchId"/>
    /// (pass both null for the Global scope, or both set for a specific branch). Precedence cascade
    /// for a given date: branch-scoped exception &gt; Global exception &gt; branch-scoped pattern
    /// (most restrictive among active branch patterns) &gt; Global pattern (most restrictive among
    /// active Global patterns). <paramref name="exceptions"/>/<paramref name="patterns"/> may contain
    /// declarations from other scopes too — anything not Global and not this exact branch is ignored.
    /// </summary>
    /// <param name="rangeStart">First date to resolve (inclusive).</param>
    /// <param name="rangeEnd">Last date to resolve (inclusive).</param>
    /// <param name="exceptions">Candidate one-off exceptions, filtered internally to this scope.</param>
    /// <param name="patterns">Candidate recurring patterns, filtered internally to this scope.</param>
    /// <param name="guildId">The guild of the target branch scope, or <c>null</c> to resolve the Global scope.</param>
    /// <param name="guildBranchId">The target branch, or <c>null</c> to resolve the Global scope.</param>
    List<ResolvedDayAvailabilityResponse> ResolveForScope(
        DateOnly rangeStart,
        DateOnly rangeEnd,
        IReadOnlyCollection<AvailabilityDeclaration> exceptions,
        IReadOnlyCollection<RecurringAvailabilityPattern> patterns,
        string? guildId,
        int? guildBranchId);
}
