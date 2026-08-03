using RaidOps.Domain.Models.Raids;

namespace RaidOps.Application.Contracts.Services;

/// <summary>
/// Bundles the "raid composition changes" cross-cutting concern — guild-local timestamp
/// resolution, audit log entry, and Discord notification dispatch — that every slot-assignment
/// command handler needs once its event is published. Extracted purely to keep each handler's own
/// constructor lean (each handler still decides *whether* to notify, based on
/// <c>RaidEvent.PublicationStatus</c>; this service only handles *how*).
/// </summary>
public interface IRaidCompositionNotifier
{
    /// <summary>A character was assigned to a slot.</summary>
    Task NotifySlotAssignedAsync(RaidEvent raidEvent, string requesterDiscordId, RaidCharacterRef character, SlotCoordinate slot, CancellationToken cancellationToken = default);

    /// <summary>A character was unassigned from a slot.</summary>
    Task NotifySlotUnassignedAsync(RaidEvent raidEvent, string requesterDiscordId, RaidCharacterRef character, SlotCoordinate slot, CancellationToken cancellationToken = default);

    /// <summary>Two characters' slots were swapped.</summary>
    Task NotifySlotsSwappedAsync(RaidEvent raidEvent, string requesterDiscordId, RaidCharacterRef characterA, SlotCoordinate slotA, RaidCharacterRef characterB, SlotCoordinate slotB, CancellationToken cancellationToken = default);

    /// <summary>A slot assignment's spec was changed.</summary>
    Task NotifySlotSpecChangedAsync(RaidEvent raidEvent, string requesterDiscordId, RaidCharacterRef character, string oldSpecName, string newSpecName, CancellationToken cancellationToken = default);
}
