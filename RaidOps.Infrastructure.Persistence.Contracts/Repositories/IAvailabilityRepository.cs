using RaidOps.Domain.Models.Calendar;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>
/// Repository contract for persisting and reading one-off availability exceptions and recurring
/// availability patterns, each independently scoped Global or to a specific guild branch. Lookups
/// by id only need the id and the owning member — scope is never part of the lookup key, since a
/// declaration's scope is fixed at creation and never used to disambiguate ownership.
/// </summary>
public interface IAvailabilityRepository
{
    /// <summary>Returns the exception identified by <paramref name="exceptionId"/>, if it belongs to <paramref name="userDiscordId"/>.</summary>
    Task<AvailabilityDeclaration?> GetExceptionByIdAsync(int exceptionId, string userDiscordId, CancellationToken cancellationToken = default);

    /// <summary>Returns every exception belonging to <paramref name="userDiscordId"/>, across every scope, that overlaps <paramref name="rangeStart"/>..<paramref name="rangeEnd"/>.</summary>
    Task<List<AvailabilityDeclaration>> GetExceptionsOverlappingAsync(string userDiscordId, DateOnly rangeStart, DateOnly rangeEnd, CancellationToken cancellationToken = default);

    /// <summary>Inserts a new exception.</summary>
    Task<AvailabilityDeclaration> AddExceptionAsync(AvailabilityDeclaration exception, CancellationToken cancellationToken = default);

    /// <summary>Deletes the exception identified by <paramref name="exceptionId"/> if it belongs to <paramref name="userDiscordId"/>. Returns <c>false</c> if no matching exception exists.</summary>
    Task<bool> DeleteExceptionAsync(int exceptionId, string userDiscordId, CancellationToken cancellationToken = default);

    /// <summary>Returns every version (current and historical) of every recurring pattern belonging to <paramref name="userDiscordId"/>, across every scope, with its days.</summary>
    Task<List<RecurringAvailabilityPattern>> GetPatternsAsync(string userDiscordId, CancellationToken cancellationToken = default);

    /// <summary>Returns the pattern identified by <paramref name="patternId"/>, with its days, if it belongs to <paramref name="userDiscordId"/>.</summary>
    Task<RecurringAvailabilityPattern?> GetPatternByIdAsync(int patternId, string userDiscordId, CancellationToken cancellationToken = default);

    /// <summary>Inserts a new recurring pattern along with its days.</summary>
    Task<RecurringAvailabilityPattern> AddPatternAsync(RecurringAvailabilityPattern pattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the pattern version identified by <paramref name="patternId"/> as of <paramref name="effectiveUntil"/>,
    /// without touching its settings or days — used when editing or stopping a pattern that has
    /// already applied to at least one past date, so that history stays intact. Returns <c>false</c>
    /// if no matching pattern exists for <paramref name="userDiscordId"/>.
    /// </summary>
    Task<bool> ClosePatternAsync(int patternId, string userDiscordId, DateOnly effectiveUntil, CancellationToken cancellationToken = default);

    /// <summary>Deletes the pattern identified by <paramref name="patternId"/> if it belongs to <paramref name="userDiscordId"/>. Returns <c>false</c> if no matching pattern exists.</summary>
    Task<bool> DeletePatternAsync(int patternId, string userDiscordId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every exception belonging to any of <paramref name="userDiscordIds"/>, across every
    /// scope (Global and every guild branch alike), that overlaps <paramref name="rangeStart"/>..
    /// <paramref name="rangeEnd"/> — a bulk counterpart to <see cref="GetExceptionsOverlappingAsync"/>
    /// used to resolve availability for many members at once (e.g. the raid board) without one query
    /// per member. Unlike a guild-scoped fetch, this correctly includes Global declarations, since
    /// the caller already knows exactly which users it cares about.
    /// </summary>
    Task<List<AvailabilityDeclaration>> GetExceptionsOverlappingForUsersAsync(IEnumerable<string> userDiscordIds, DateOnly rangeStart, DateOnly rangeEnd, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every version (current and historical) of every recurring pattern belonging to any of
    /// <paramref name="userDiscordIds"/>, across every scope, with their days — a bulk counterpart to
    /// <see cref="GetPatternsAsync"/>, same Global-inclusive behavior as
    /// <see cref="GetExceptionsOverlappingForUsersAsync"/>.
    /// </summary>
    Task<List<RecurringAvailabilityPattern>> GetPatternsForUsersAsync(IEnumerable<string> userDiscordIds, CancellationToken cancellationToken = default);
}
