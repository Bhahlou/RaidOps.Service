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
    /// Returns every exception with a non-null <see cref="AvailabilityDeclaration.GuildId"/> equal to
    /// <paramref name="guildId"/> that overlaps <paramref name="rangeStart"/>..<paramref name="rangeEnd"/> —
    /// a bulk, guild-wide counterpart to <see cref="GetExceptionsOverlappingAsync"/> used to resolve
    /// availability for many members at once (e.g. the raid board) without one query per member.
    /// Does not include Global-scoped declarations (<c>GuildId == null</c>) — see the raid-builder
    /// catch-up note on resolving those too before this is relied on for a real absence check.
    /// </summary>
    Task<List<AvailabilityDeclaration>> GetExceptionsOverlappingForGuildAsync(string guildId, DateOnly rangeStart, DateOnly rangeEnd, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every version (current and historical) of every recurring pattern with a non-null
    /// <see cref="RecurringAvailabilityPattern.GuildId"/> equal to <paramref name="guildId"/>, with
    /// their days — a bulk, guild-wide counterpart to <see cref="GetPatternsAsync"/>. Does not include
    /// Global-scoped patterns, same caveat as <see cref="GetExceptionsOverlappingForGuildAsync"/>.
    /// </summary>
    Task<List<RecurringAvailabilityPattern>> GetPatternsForGuildAsync(string guildId, CancellationToken cancellationToken = default);
}
