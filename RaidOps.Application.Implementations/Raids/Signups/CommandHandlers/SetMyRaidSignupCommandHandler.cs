using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Signups.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Signups.CommandHandlers;

/// <summary>
/// Handles <see cref="SetMyRaidSignupCommand"/> by upserting the requester's own response to a
/// Signup-mode raid event — allowed regardless of the event's Draft/Published status, since the
/// signup call is posted from creation and responses are meant to be gathered before the raid is
/// even built. Any slot the requester currently occupies in this event whose character no longer
/// matches the new response is automatically unassigned — whether the new response isn't
/// <see cref="SignupStatus.Accepted"/> at all, or is Accepted but with a different character than
/// the one seated — same audit-log/Discord side-effects as an officer manually unassigning them,
/// just triggered by the player's own RSVP change instead. Refreshes the standing signup-call
/// embed on every change.
/// </summary>
public class SetMyRaidSignupCommandHandler(
    IGuildAccessService guildAccessService,
    IRaidEventRepository raidEventRepository,
    ICharacterRepository characterRepository,
    IGuildMembershipRepository guildMembershipRepository,
    IRaidSignupRepository raidSignupRepository,
    IRaidSlotUnassignmentService raidSlotUnassignmentService,
    IRaidSignupChangeNotifier raidSignupChangeNotifier) : ICommandHandlerAsync<SetMyRaidSignupCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(SetMyRaidSignupCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, command.GuildBranchId, cancellationToken);
        if (accessLevel < GuildAccessLevel.Roster)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not on this guild branch's roster.");

        var raidEvent = await raidEventRepository.GetByIdAsync(command.EventId, command.GuildBranchId, cancellationToken);
        if (raidEvent == null)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidEventNotFound, $"Raid event '{command.EventId}' does not exist.");

        if (raidEvent.SignupMode != SignupMode.Signup)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidEventNotInSignupMode, "This raid event is not in Signup mode.");

        // Deliberately no Published gate — the whole point of Signup mode is to gather responses
        // *before* the raid is built, so signups must be possible while the event is still a Draft.

        var characterResult = await ResolveCharacterAndSpecAsync(command, cancellationToken);
        if (characterResult.IsFailed)
            return Result<CommandResponse>.Fail(characterResult.Error!, characterResult.Detail);

        var (characterId, specId) = characterResult.Value;

        await raidSignupRepository.SetSignupAsync(new RaidSignup
        {
            RaidEventId = command.EventId,
            UserDiscordId = command.RequesterDiscordId,
            Status = command.Status,
            CharacterId = characterId,
            SpecId = specId,
            RespondedAtUtc = DateTime.UtcNow,
        }, cancellationToken);

        await UnassignExistingSlotsAsync(raidEvent, command.RequesterDiscordId, characterId, cancellationToken);

        await raidSignupChangeNotifier.NotifyChangedAsync(raidEvent, cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Signup response saved."));
    }

    /// <summary>
    /// Validates and resolves the (character, spec) pair to record for the signup, when one is
    /// required — <see cref="SignupStatus.Accepted"/>/<see cref="SignupStatus.Tentative"/> only,
    /// every other status returns <c>(null, null)</c> unconditionally.
    /// </summary>
    private async Task<Result<(int? CharacterId, int? SpecId)>> ResolveCharacterAndSpecAsync(SetMyRaidSignupCommand command, CancellationToken cancellationToken)
    {
        if (command.Status is not (SignupStatus.Accepted or SignupStatus.Tentative))
            return Result<(int?, int?)>.Ok((null, null));

        if (command.CharacterId is not { } candidateCharacterId)
            return Result<(int?, int?)>.Fail(ResponseDetail.CharacterRequiredForSignup, "A character must be chosen to respond to this raid's signup.");

        var character = await characterRepository.GetByIdAsync(candidateCharacterId, cancellationToken);
        if (character == null)
            return Result<(int?, int?)>.Fail(ResponseDetail.CharacterNotFound, $"Character '{candidateCharacterId}' does not exist.");

        if (character.UserDiscordId != command.RequesterDiscordId)
            return Result<(int?, int?)>.Fail(ResponseDetail.CharacterNotOwned, "This character does not belong to the requester.");

        var memberships = await guildMembershipRepository.GetByCharacterIdAsync(candidateCharacterId, cancellationToken);
        if (!memberships.Any(m => m.GuildBranchId == command.GuildBranchId))
            return Result<(int?, int?)>.Fail(ResponseDetail.CharacterNotOnRoster, "Character is not an active member of this guild branch's roster.");

        if (command.SpecId is not { } candidateSpecId)
            return Result<(int?, int?)>.Fail(ResponseDetail.SpecRequiredForSignup, "A spec must be chosen to respond to this raid's signup.");

        var raidSpecs = await characterRepository.GetRaidSpecsAsync(candidateCharacterId, cancellationToken);
        if (!raidSpecs.Any(s => s.SpecId == candidateSpecId))
            return Result<(int?, int?)>.Fail(ResponseDetail.SpecNotAvailableForCharacter, "This spec is not one of the character's declared raid specs.");

        return Result<(int?, int?)>.Ok((candidateCharacterId, candidateSpecId));
    }

    /// <summary>
    /// Unassigns every slot the requester currently occupies in this event whose character no
    /// longer matches their new response — covers both a status change away from Accepted
    /// (<paramref name="keepCharacterId"/> is <c>null</c>, so every existing occupied slot goes)
    /// and staying Accepted but swapping to a different character (only that stale slot goes; a
    /// re-confirm of the same character leaves its slot untouched).
    /// </summary>
    private async Task UnassignExistingSlotsAsync(RaidEvent raidEvent, string requesterDiscordId, int? keepCharacterId, CancellationToken cancellationToken)
    {
        var ownAssignments = raidEvent.Assignments
            .Where(a => a.AssignedPlayerDiscordId == requesterDiscordId && a.CharacterId != keepCharacterId)
            .ToList();

        foreach (var occupant in ownAssignments)
            await raidSlotUnassignmentService.UnassignAsync(raidEvent, occupant.GroupNumber, occupant.SlotNumber, requesterDiscordId, cancellationToken);
    }
}
