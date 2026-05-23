using RaidOps.Domain.Models.Character;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>Persistence for WoW characters and their per-expansion states.</summary>
public interface ICharacterRepository
{
    /// <summary>
    /// Returns all characters owned by the given user, including their realm, class, race,
    /// and expansion states. Ordered alphabetically by name.
    /// </summary>
    Task<IEnumerable<Character>> GetByUserWithDetailsAsync(string userDiscordId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the set of BNet character IDs already imported by the given user.
    /// Used to mark characters as already present when displaying the import selection UI.
    /// </summary>
    Task<HashSet<long>> GetBnetIdsByUserAsync(string userDiscordId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates the character identified by its BNet character ID.
    /// Returns the persisted entity (with its DB-generated <c>Id</c>).
    /// </summary>
    Task<Character> UpsertAsync(Character character, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates the expansion state for a (character × expansion) pair.
    /// </summary>
    Task UpsertExpansionStateAsync(CharacterExpansionState state, CancellationToken cancellationToken = default);
}
