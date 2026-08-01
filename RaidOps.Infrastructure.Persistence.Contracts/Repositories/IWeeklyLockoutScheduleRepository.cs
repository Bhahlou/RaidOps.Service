using RaidOps.Domain.Models.Raids;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>Read-only access to the static <see cref="WeeklyLockoutSchedule"/> reference table (one row per Blizzard API region).</summary>
public interface IWeeklyLockoutScheduleRepository
{
    /// <summary>Returns the schedule for the given region ("eu", "us", "kr", "tw"), or <c>null</c> if none is seeded for it.</summary>
    Task<WeeklyLockoutSchedule?> GetByRegionAsync(string region, CancellationToken cancellationToken = default);
}
