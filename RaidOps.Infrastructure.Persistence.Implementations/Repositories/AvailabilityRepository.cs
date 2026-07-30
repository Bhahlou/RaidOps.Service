using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Models.Calendar;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Infrastructure.Persistence.Implementations.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IAvailabilityRepository"/>.
/// Handles both one-off exceptions and recurring patterns without a base-class dependency,
/// since a recurring pattern's day set must be replaced as a unit rather than CRUD'd row by row.
/// </summary>
public class AvailabilityRepository(RaidOpsDbContext context) : IAvailabilityRepository
{
    /// <inheritdoc/>
    public async Task<AvailabilityDeclaration?> GetExceptionByIdAsync(int exceptionId, string userDiscordId, CancellationToken cancellationToken = default)
        => await context.AvailabilityExceptions
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == exceptionId && e.UserDiscordId == userDiscordId, cancellationToken);

    /// <inheritdoc/>
    public async Task<List<AvailabilityDeclaration>> GetExceptionsOverlappingAsync(string userDiscordId, DateOnly rangeStart, DateOnly rangeEnd, CancellationToken cancellationToken = default)
        => await context.AvailabilityExceptions
            .Where(e => e.UserDiscordId == userDiscordId && e.StartDate <= rangeEnd && e.EndDate >= rangeStart)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<AvailabilityDeclaration> AddExceptionAsync(AvailabilityDeclaration exception, CancellationToken cancellationToken = default)
    {
        context.AvailabilityExceptions.Add(exception);
        await context.SaveChangesAsync(cancellationToken);
        return exception;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteExceptionAsync(int exceptionId, string userDiscordId, CancellationToken cancellationToken = default)
    {
        var exception = await context.AvailabilityExceptions
            .FirstOrDefaultAsync(e => e.Id == exceptionId && e.UserDiscordId == userDiscordId, cancellationToken);
        if (exception == null) return false;

        context.AvailabilityExceptions.Remove(exception);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc/>
    public async Task<List<RecurringAvailabilityPattern>> GetPatternsAsync(string userDiscordId, CancellationToken cancellationToken = default)
        => await context.RecurringAvailabilityPatterns
            .Where(p => p.UserDiscordId == userDiscordId)
            .Include(p => p.Days)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<RecurringAvailabilityPattern?> GetPatternByIdAsync(int patternId, string userDiscordId, CancellationToken cancellationToken = default)
        => await context.RecurringAvailabilityPatterns
            .Where(p => p.Id == patternId && p.UserDiscordId == userDiscordId)
            .Include(p => p.Days)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<RecurringAvailabilityPattern> AddPatternAsync(RecurringAvailabilityPattern pattern, CancellationToken cancellationToken = default)
    {
        context.RecurringAvailabilityPatterns.Add(pattern);
        await context.SaveChangesAsync(cancellationToken);
        return pattern;
    }

    /// <inheritdoc/>
    public async Task<bool> ClosePatternAsync(int patternId, string userDiscordId, DateOnly effectiveUntil, CancellationToken cancellationToken = default)
    {
        var pattern = await context.RecurringAvailabilityPatterns
            .FirstOrDefaultAsync(p => p.Id == patternId && p.UserDiscordId == userDiscordId, cancellationToken);
        if (pattern == null) return false;

        pattern.EffectiveUntil = effectiveUntil;
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> DeletePatternAsync(int patternId, string userDiscordId, CancellationToken cancellationToken = default)
    {
        var pattern = await context.RecurringAvailabilityPatterns
            .FirstOrDefaultAsync(p => p.Id == patternId && p.UserDiscordId == userDiscordId, cancellationToken);
        if (pattern == null) return false;

        context.RecurringAvailabilityPatterns.Remove(pattern);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
