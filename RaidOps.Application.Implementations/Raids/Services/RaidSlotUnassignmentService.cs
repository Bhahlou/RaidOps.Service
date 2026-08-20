using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Services;

/// <inheritdoc/>
public class RaidSlotUnassignmentService(
    ICharacterRepository characterRepository,
    IRaidCompositionRepository raidCompositionRepository,
    IRaidCompositionNotifier raidCompositionNotifier) : IRaidSlotUnassignmentService
{
    /// <inheritdoc/>
    public async Task<bool> UnassignAsync(RaidEvent raidEvent, int groupNumber, int slotNumber, string requesterDiscordId, CancellationToken cancellationToken = default)
    {
        var occupant = raidEvent.Assignments.FirstOrDefault(a => a.GroupNumber == groupNumber && a.SlotNumber == slotNumber);

        var unassigned = await raidCompositionRepository.UnassignAsync(raidEvent.Id, groupNumber, slotNumber, cancellationToken);
        if (!unassigned)
            return false;

        if (raidEvent.PublicationStatus == RaidPublicationStatus.Published && occupant != null)
        {
            var character = await characterRepository.GetByIdAsync(occupant.CharacterId, cancellationToken);
            var characterName = character?.Name ?? occupant.CharacterId.ToString();
            var raidSpecs = await characterRepository.GetRaidSpecsAsync(occupant.CharacterId, cancellationToken);
            var specName = raidSpecs.FirstOrDefault(s => s.SpecId == occupant.SpecId)?.Spec.Name;

            await raidCompositionNotifier.NotifySlotUnassignedAsync(
                raidEvent, requesterDiscordId,
                new RaidCharacterRef(characterName, character?.ClassId, specName),
                occupant.AssignedPlayerDiscordId,
                new SlotCoordinate(groupNumber, slotNumber),
                cancellationToken);
        }

        return true;
    }
}
