using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>
/// Repository contract for managing <see cref="GuildMembership"/> roster entries.
/// </summary>
public interface IGuildMembershipRepository
{
    /// <summary>
    /// Returns all guild memberships for the given character, including Guild navigation data.
    /// </summary>
    /// <param name="characterId">Internal character ID.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task<List<GuildMembership>> GetByCharacterIdAsync(int characterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all guild memberships for the given set of characters in a single query.
    /// Used for bulk eligibility checks to avoid N+1 per-character fetches.
    /// </summary>
    /// <param name="characterIds">Internal character IDs to query.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task<List<GuildMembership>> GetByCharacterIdsAsync(IEnumerable<int> characterIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all memberships in the given guild, including Character (with Realm and Class) navigation data.
    /// </summary>
    /// <param name="guildId">Discord snowflake ID of the guild.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task<List<GuildMembership>> GetByGuildIdAsync(string guildId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <c>true</c> if the character is already on the specified guild's roster.
    /// </summary>
    /// <param name="characterId">Internal character ID.</param>
    /// <param name="guildId">Discord snowflake ID of the guild.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task<bool> ExistsAsync(int characterId, string guildId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a character to a guild's roster.
    /// </summary>
    /// <param name="membership">The membership record to persist.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task AddAsync(GuildMembership membership, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the membership for the given character/guild pair, or <c>null</c> if not found.
    /// </summary>
    /// <param name="characterId">Internal character ID.</param>
    /// <param name="guildId">Discord snowflake ID of the guild.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task<GuildMembership?> GetAsync(int characterId, string guildId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the raid-composition rank of a character on a guild's roster.
    /// </summary>
    /// <param name="characterId">Internal character ID.</param>
    /// <param name="guildId">Discord snowflake ID of the guild.</param>
    /// <param name="rank">The new rank to assign.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns><c>true</c> if the membership existed and was updated; <c>false</c> if not found.</returns>
    Task<bool> UpdateRankAsync(int characterId, string guildId, CharacterRank rank, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a character from a guild's roster.
    /// </summary>
    /// <param name="characterId">Internal character ID.</param>
    /// <param name="guildId">Discord snowflake ID of the guild.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns><c>true</c> if the membership existed and was removed; <c>false</c> if not found.</returns>
    Task<bool> DeleteAsync(int characterId, string guildId, CancellationToken cancellationToken = default);
}