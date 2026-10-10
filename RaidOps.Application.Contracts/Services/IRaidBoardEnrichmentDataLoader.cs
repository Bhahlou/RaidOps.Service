using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Raids;

namespace RaidOps.Application.Contracts.Services;

/// <summary>
/// Bulk-loads every piece of per-event enrichment data <c>GetRaidBoardQueryHandler</c> needs to map
/// a range of raid events to a board response — player names, roster availability, signup
/// responses, and assigned characters' declared raid specs — each in its own bulk read scoped to
/// the events/roster already resolved by the caller, rather than one query per event/assignment.
/// </summary>
public interface IRaidBoardEnrichmentDataLoader
{
    Task<RaidBoardEnrichmentData> LoadAsync(
        List<RaidEvent> events, List<string> rosterPlayerIds, string guildId, int guildBranchId,
        DateOnly rangeStart, DateOnly rangeEnd, CancellationToken cancellationToken = default);
}

/// <summary>Everything <see cref="IRaidBoardEnrichmentDataLoader"/> resolves for one board response.</summary>
public sealed record RaidBoardEnrichmentData(
    Dictionary<string, User> PlayersById,
    IRaidAvailabilityLookup AvailabilityLookup,
    Dictionary<int, Dictionary<string, RaidSignup>> SignupsByEvent,
    Dictionary<int, List<CharacterRaidSpec>> RaidSpecsByCharacter);
