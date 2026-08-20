using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Services;

/// <inheritdoc/>
public class RaidBoardEnrichmentDataLoader(
    IRaidAvailabilityService raidAvailabilityService,
    IRaidSignupRepository raidSignupRepository,
    IUsersRepository usersRepository,
    ICharacterRepository characterRepository) : IRaidBoardEnrichmentDataLoader
{
    /// <inheritdoc/>
    public async Task<RaidBoardEnrichmentData> LoadAsync(
        List<RaidEvent> events, List<string> rosterPlayerIds, string guildId, int guildBranchId,
        DateOnly rangeStart, DateOnly rangeEnd, CancellationToken cancellationToken = default)
    {
        var availabilityLookup = await raidAvailabilityService.LoadRosterAvailabilityAsync(rosterPlayerIds, guildId, guildBranchId, rangeStart, rangeEnd, cancellationToken);

        var signupModeEventIds = events.Where(e => e.SignupMode == SignupMode.Signup).Select(e => e.Id).ToList();
        var signups = await raidSignupRepository.GetForEventsAsync(signupModeEventIds, cancellationToken);
        var signupsByEvent = signups
            .GroupBy(s => s.RaidEventId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(s => s.UserDiscordId, s => s));

        var assignedPlayerIds = events.SelectMany(e => e.Assignments).Select(a => a.AssignedPlayerDiscordId).Distinct().ToList();
        var players = await usersRepository.FindAsync(u => assignedPlayerIds.Contains(u.DiscordId), cancellationToken);
        var playersById = players.ToDictionary(u => u.DiscordId);

        var assignedCharacterIds = events.SelectMany(e => e.Assignments).Select(a => a.CharacterId).Distinct().ToList();
        var raidSpecs = await characterRepository.GetRaidSpecsForCharactersAsync(assignedCharacterIds, cancellationToken);
        var raidSpecsByCharacter = raidSpecs
            .GroupBy(rs => rs.CharacterId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return new RaidBoardEnrichmentData(playersById, availabilityLookup, signupsByEvent, raidSpecsByCharacter);
    }
}
