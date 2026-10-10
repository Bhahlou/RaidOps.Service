using RaidOps.Domain.Models.Raids.Attributions;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>Repository contract for <see cref="RaidEventAttribution"/> persistence.</summary>
public interface IRaidEventAttributionsRepository
{
    /// <summary>Returns every fill of the given raid event.</summary>
    Task<List<RaidEventAttribution>> GetForEventAsync(int raidEventId, CancellationToken cancellationToken = default);

    /// <summary>Inserts or updates the fill at (<paramref name="raidEventId"/>, <paramref name="cellId"/>, <paramref name="instanceIndex"/>) with the given character.</summary>
    Task SetAsync(int raidEventId, int cellId, int definitionId, int instanceIndex, int characterId, string assignedByDiscordId, CancellationToken cancellationToken = default);

    /// <summary>Deletes the fill at the given coordinate. Returns <c>false</c> if it was already empty.</summary>
    Task<bool> ClearAsync(int raidEventId, int cellId, int instanceIndex, CancellationToken cancellationToken = default);
}
