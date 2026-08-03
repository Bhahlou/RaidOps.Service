using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Services;

/// <inheritdoc cref="IRaidSlotEligibilityValidator"/>
public class RaidSlotEligibilityValidator(
    IGuildMembershipRepository guildMembershipRepository,
    IRaidAvailabilityService raidAvailabilityService,
    IRaidLockoutConflictChecker raidLockoutConflictChecker) : IRaidSlotEligibilityValidator
{
    /// <inheritdoc/>
    public async Task<Result<bool>> ValidateRosterMembershipAsync(int characterId, int guildBranchId, CancellationToken cancellationToken = default)
    {
        var memberships = await guildMembershipRepository.GetByCharacterIdAsync(characterId, cancellationToken);
        if (!memberships.Any(m => m.GuildBranchId == guildBranchId))
            return Result<bool>.Fail(ResponseDetail.CharacterNotOnRoster, "Character is not an active member of this guild branch's roster.");

        return Result<bool>.Ok(true);
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> ValidateAssignabilityAsync(RaidEvent raidEvent, Character character, string guildId, int guildBranchId, CancellationToken cancellationToken = default)
    {
        var isUnavailable = await raidAvailabilityService.IsPlayerUnavailableAsync(character.UserDiscordId, guildId, guildBranchId, raidEvent.StartsAtUtc, cancellationToken);
        if (isUnavailable)
            return Result<bool>.Fail(ResponseDetail.MemberDeclaredAbsent, "This member's declared availability does not cover the event's start time.");

        var conflictingZoneName = await raidLockoutConflictChecker.FindConflictingZoneNameAsync(raidEvent, character.Id, guildId, guildBranchId, cancellationToken);
        if (conflictingZoneName != null)
            return Result<bool>.Fail(ResponseDetail.RaidLockoutConflict, $"Character is already locked to '{conflictingZoneName}' for this reset window via another event.");

        return Result<bool>.Ok(true);
    }
}
