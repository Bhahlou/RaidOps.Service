using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Calendar;

namespace RaidOps.Application.Contracts.Services;

/// <summary>
/// Compares a member's resolved availability before and after a mutation and announces only the
/// net change — one audit log entry and one Discord notification per contiguous segment of days
/// that actually flipped between "available" and "restricted" (<see cref="DayAvailabilityStatus.Absent"/>
/// or <see cref="DayAvailabilityStatus.Partial"/>). Mechanism-agnostic: callers just supply the
/// exception rows as they stood immediately before and immediately after their own mutation — a
/// pure create/delete naturally collapses to a single segment matching today's behavior, while an
/// edit (internally a delete + re-create) produces exactly the segments that actually changed,
/// instead of one spurious "everything removed" plus one spurious "everything re-added".
/// </summary>
public interface IAvailabilityChangeAnnouncer
{
    /// <summary>
    /// Diffs <see cref="AvailabilityChange.BeforeExceptions"/> against
    /// <see cref="AvailabilityChange.AfterExceptions"/> over <see cref="AvailabilityChange.WindowStart"/>–
    /// <see cref="AvailabilityChange.WindowEnd"/> and announces each resulting segment. No-ops
    /// entirely if nothing actually changed within the window.
    /// </summary>
    Task AnnounceAsync(AvailabilityChange change, CancellationToken cancellationToken = default);
}

/// <summary>
/// The before/after snapshot an <see cref="IAvailabilityChangeAnnouncer"/> diffs to determine what
/// actually changed. Bundled into one type rather than passed as separate parameters — callers just
/// supply the exception rows as they stood immediately before and immediately after their own
/// mutation, plus the patterns that fill in the gaps.
/// </summary>
/// <param name="GuildId">The guild the change happened in.</param>
/// <param name="RequesterDiscordId">The member whose availability changed.</param>
/// <param name="WindowStart">Start of the date range to diff, inclusive.</param>
/// <param name="WindowEnd">End of the date range to diff, inclusive.</param>
/// <param name="BeforeExceptions">Exception rows as they stood immediately before the mutation.</param>
/// <param name="AfterExceptions">Exception rows as they stand immediately after the mutation.</param>
/// <param name="Patterns">Recurring patterns in effect, used to resolve each day's baseline status.</param>
public record AvailabilityChange(
    string GuildId,
    string RequesterDiscordId,
    DateOnly WindowStart,
    DateOnly WindowEnd,
    IReadOnlyCollection<AvailabilityDeclaration> BeforeExceptions,
    IReadOnlyCollection<AvailabilityDeclaration> AfterExceptions,
    IReadOnlyCollection<RecurringAvailabilityPattern> Patterns);
