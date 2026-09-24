using RaidOps.Application.Contracts.CQRS;
using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Raids.CompositionPreviews.Commands;

/// <summary>
/// Upserts a single (group, slot) coordinate of a preview's grid — sets its class/spec placeholder
/// and/or free-text note. If all three of <see cref="WowClassId"/>/<see cref="SpecId"/>/
/// <see cref="Note"/> end up null, the slot row is deleted instead (kept sparse, same convention
/// as <see cref="Domain.Models.Raids.RaidSlotAssignment"/>). The requesting user must hold
/// <see cref="GuildAccessLevel.Officer"/> access on the guild branch.
/// </summary>
public class UpdateRaidCompositionPreviewSlotCommand : ICommandRequest
{
    /// <summary>Discord snowflake ID of the guild this preview belongs to. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>Surrogate ID of the guild branch this preview belongs to. Set by the controller from the route, not from the request body.</summary>
    public int GuildBranchId { get; set; }

    /// <summary>Discord snowflake ID of the requesting officer. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>Surrogate ID of the preview whose slot is being edited. Set by the controller from the route, not from the request body.</summary>
    public int PreviewId { get; set; }

    /// <summary>1-based group number within the preview's grid.</summary>
    public required int GroupNumber { get; set; }

    /// <summary>1-based slot number within the group.</summary>
    public required int SlotNumber { get; set; }

    /// <summary>Placeholder class for this slot, or <c>null</c> to clear it.</summary>
    public int? WowClassId { get; set; }

    /// <summary>Placeholder spec for this slot, or <c>null</c> to clear it. When set, must belong to <see cref="WowClassId"/>.</summary>
    public int? SpecId { get; set; }

    /// <summary>Free-text note, or <c>null</c> to clear it. Unrestricted content.</summary>
    public string? Note { get; set; }
}
