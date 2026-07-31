using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>Repository contract for persisting and reading <see cref="RaidEvent"/> occurrences.</summary>
public interface IRaidEventRepository
{
    /// <summary>Returns the event identified by <paramref name="id"/> on <paramref name="guildBranchId"/>, including its target zones and assignments, or <c>null</c> if not found.</summary>
    Task<RaidEvent?> GetByIdAsync(int id, int guildBranchId, CancellationToken cancellationToken = default);

    /// <summary>Returns every event of <paramref name="guildBranchId"/> that starts within the given UTC range (including cancelled ones, so the board can still show them as cancelled), with their target zones and assignments.</summary>
    Task<List<RaidEvent>> GetForGuildBranchInRangeAsync(int guildBranchId, DateTime rangeStartUtc, DateTime rangeEndUtc, CancellationToken cancellationToken = default);

    /// <summary>Returns <c>true</c> if an event already exists for the given series at the given UTC start time — backs idempotent materialization.</summary>
    Task<bool> ExistsForSeriesAndDateAsync(int raidSeriesId, DateTime startsAtUtc, CancellationToken cancellationToken = default);

    /// <summary>Inserts a new event along with its target zones.</summary>
    Task<RaidEvent> AddAsync(RaidEvent raidEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the scalar fields of the event identified by <paramref name="raidEvent"/>.<see cref="RaidEvent.Id"/>
    /// and replaces its target-zone set atomically. Returns <c>false</c> if no matching event exists on <paramref name="guildBranchId"/>.
    /// </summary>
    Task<bool> UpdateAsync(RaidEvent raidEvent, int guildBranchId, IEnumerable<int> raidZoneIds, CancellationToken cancellationToken = default);

    /// <summary>Sets <see cref="RaidEvent.Status"/> to <see cref="RaidEventStatus.Cancelled"/>. Returns <c>false</c> if no matching event exists on <paramref name="guildBranchId"/>.</summary>
    Task<bool> CancelAsync(int id, int guildBranchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets <see cref="RaidEvent.PublicationStatus"/> to <see cref="RaidPublicationStatus.Published"/>,
    /// stamping <see cref="RaidEvent.PublishedAt"/> (UTC now) and <see cref="RaidEvent.PublishedByDiscordId"/>.
    /// Returns <c>false</c> if no matching event exists on <paramref name="guildBranchId"/>.
    /// </summary>
    Task<bool> PublishAsync(int id, int guildBranchId, string publishedByDiscordId, CancellationToken cancellationToken = default);

    /// <summary>Deletes the event identified by <paramref name="id"/> on <paramref name="guildBranchId"/>. Returns <c>false</c> if no matching event exists.</summary>
    Task<bool> DeleteAsync(int id, int guildBranchId, CancellationToken cancellationToken = default);
}
