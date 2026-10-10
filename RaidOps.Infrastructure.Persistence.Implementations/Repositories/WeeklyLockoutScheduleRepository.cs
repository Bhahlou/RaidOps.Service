using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Infrastructure.Persistence.Implementations.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IWeeklyLockoutScheduleRepository"/>.
/// Reads the seeded <see cref="WeeklyLockoutSchedule"/> reference table — no writes at runtime.
/// </summary>
public class WeeklyLockoutScheduleRepository(RaidOpsDbContext context) : IWeeklyLockoutScheduleRepository
{
    /// <inheritdoc/>
    public async Task<WeeklyLockoutSchedule?> GetByRegionAsync(string region, CancellationToken cancellationToken = default)
        => await context.WeeklyLockoutSchedules
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Region == region, cancellationToken);
}
