namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>What changed as a result of a <see cref="ISpellRepository.UpsertAsync"/> call.</summary>
public class SpellSyncDiff
{
    /// <summary>Spells that didn't exist before this upsert.</summary>
    public List<SpellSyncEntry> Added { get; set; } = [];

    /// <summary>Spells that already existed but whose English name changed.</summary>
    public List<SpellSyncEntry> Renamed { get; set; } = [];
}
