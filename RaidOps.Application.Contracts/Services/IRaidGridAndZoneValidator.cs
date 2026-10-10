using RaidOps.Application.Contracts.Common;

namespace RaidOps.Application.Contracts.Services;

/// <summary>
/// Validates the shared prologue of every raid series/event create/update command: the requester
/// holds <see cref="Domain.Enums.GuildAccessLevel.Officer"/> access on the guild branch, the
/// requested grid shape is positive, and every requested raid zone ID actually exists.
/// </summary>
public interface IRaidGridAndZoneValidator
{
    /// <summary>
    /// Runs the checks in a fixed order (access, grid shape, zone set) so the first failing rule
    /// always produces the same error for a given bad request. Returns the deduplicated raid zone
    /// ID list on success.
    /// </summary>
    Task<Result<List<int>>> ValidateAsync(
        string requesterDiscordId,
        string guildId,
        int guildBranchId,
        int groupCount,
        int slotsPerGroup,
        IEnumerable<int> raidZoneIds,
        CancellationToken cancellationToken = default);
}
