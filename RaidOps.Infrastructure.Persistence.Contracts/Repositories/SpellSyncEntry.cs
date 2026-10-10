namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>One spell added or renamed by a <see cref="ISpellRepository.UpsertAsync"/> call.</summary>
public class SpellSyncEntry
{
    /// <summary>Blizzard's spell ID.</summary>
    public required int SpellId { get; set; }

    /// <summary>The spell's English name after the upsert.</summary>
    public required string NameEn { get; set; }

    /// <summary>The spell's English name before the upsert. Null for a newly-inserted spell.</summary>
    public string? PreviousNameEn { get; set; }
}
