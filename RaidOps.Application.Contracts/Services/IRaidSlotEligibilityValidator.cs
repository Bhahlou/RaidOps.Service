using RaidOps.Application.Contracts.Common;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Raids;

namespace RaidOps.Application.Contracts.Services;

/// <summary>
/// Validates the three independent eligibility rules a character must clear to occupy a raid event
/// slot: it belongs to the target guild branch's roster, its player hasn't declared themselves
/// unavailable for the event's start time, and it holds no lockout conflict with another event on a
/// shared raid zone. Split into two methods rather than one combined call so
/// <c>AssignCharacterToSlotCommandHandler</c> can keep running its checks in the same fixed order it
/// always has — roster membership before grid/occupancy checks, availability/lockout after — while
/// only holding one collaborator for all three rules instead of three.
/// </summary>
public interface IRaidSlotEligibilityValidator
{
    /// <summary>Returns a failed <see cref="ResponseDetail.CharacterNotOnRoster"/> result unless <paramref name="characterId"/> is an active roster member of <paramref name="guildBranchId"/>.</summary>
    Task<Result<bool>> ValidateRosterMembershipAsync(int characterId, int guildBranchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a failed <see cref="ResponseDetail.MemberDeclaredAbsent"/> or
    /// <see cref="ResponseDetail.RaidLockoutConflict"/> result if <paramref name="character"/>'s
    /// player is unavailable at <paramref name="raidEvent"/>'s start time, or the character already
    /// holds a lockout on one of the event's target zones via another event.
    /// </summary>
    Task<Result<bool>> ValidateAssignabilityAsync(RaidEvent raidEvent, Character character, string guildId, int guildBranchId, CancellationToken cancellationToken = default);
}
