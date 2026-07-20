using RaidOps.Domain.Models.Calendar;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>
/// Repository contract for persisting and reading one-off availability exceptions and recurring
/// availability patterns, scoped to a member's participation in a specific guild.
/// </summary>
public interface IAvailabilityRepository
{
    /// <summary>Returns the exception identified by <paramref name="exceptionId"/>, if it belongs to <paramref name="userDiscordId"/> in <paramref name="guildId"/>.</summary>
    Task<AvailabilityDeclaration?> GetExceptionByIdAsync(int exceptionId, string userDiscordId, string guildId, CancellationToken cancellationToken = default);

    /// <summary>Returns every exception belonging to <paramref name="userDiscordId"/> in <paramref name="guildId"/> that overlaps <paramref name="rangeStart"/>..<paramref name="rangeEnd"/>.</summary>
    Task<List<AvailabilityDeclaration>> GetExceptionsOverlappingAsync(string userDiscordId, string guildId, DateOnly rangeStart, DateOnly rangeEnd, CancellationToken cancellationToken = default);

    /// <summary>Inserts a new exception.</summary>
    Task<AvailabilityDeclaration> AddExceptionAsync(AvailabilityDeclaration exception, CancellationToken cancellationToken = default);

    /// <summary>Deletes the exception identified by <paramref name="exceptionId"/> if it belongs to <paramref name="userDiscordId"/> in <paramref name="guildId"/>. Returns <c>false</c> if no matching exception exists.</summary>
    Task<bool> DeleteExceptionAsync(int exceptionId, string userDiscordId, string guildId, CancellationToken cancellationToken = default);

    /// <summary>Returns every version (current and historical) of every recurring pattern belonging to <paramref name="userDiscordId"/> in <paramref name="guildId"/>, with its days.</summary>
    Task<List<RecurringAvailabilityPattern>> GetPatternsAsync(string userDiscordId, string guildId, CancellationToken cancellationToken = default);

    /// <summary>Returns the pattern identified by <paramref name="patternId"/>, with its days, if it belongs to <paramref name="userDiscordId"/> in <paramref name="guildId"/>.</summary>
    Task<RecurringAvailabilityPattern?> GetPatternByIdAsync(int patternId, string userDiscordId, string guildId, CancellationToken cancellationToken = default);

    /// <summary>Inserts a new recurring pattern along with its days.</summary>
    Task<RecurringAvailabilityPattern> AddPatternAsync(RecurringAvailabilityPattern pattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the pattern version identified by <paramref name="patternId"/> as of <paramref name="effectiveUntil"/>,
    /// without touching its settings or days — used when editing or stopping a pattern that has
    /// already applied to at least one past date, so that history stays intact. Returns <c>false</c>
    /// if no matching pattern exists for <paramref name="userDiscordId"/> in <paramref name="guildId"/>.
    /// </summary>
    Task<bool> ClosePatternAsync(int patternId, string userDiscordId, string guildId, DateOnly effectiveUntil, CancellationToken cancellationToken = default);

    /// <summary>Deletes the pattern identified by <paramref name="patternId"/> if it belongs to <paramref name="userDiscordId"/> in <paramref name="guildId"/>. Returns <c>false</c> if no matching pattern exists.</summary>
    Task<bool> DeletePatternAsync(int patternId, string userDiscordId, string guildId, CancellationToken cancellationToken = default);
}
