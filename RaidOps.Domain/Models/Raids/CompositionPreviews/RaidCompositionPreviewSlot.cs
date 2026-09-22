using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RaidOps.Domain.Models.Reference;

namespace RaidOps.Domain.Models.Raids.CompositionPreviews;

/// <summary>
/// A single (group, slot) coordinate of a <see cref="RaidCompositionPreview"/>'s grid. Storage is
/// sparse — a coordinate with no row is an empty slot, same convention as
/// <see cref="RaidSlotAssignment"/> — the grid's shape is implied by
/// <see cref="RaidCompositionPreview.GroupCount"/> x <see cref="RaidCompositionPreview.SlotsPerGroup"/>.
/// Unlike <see cref="RaidSlotAssignment"/>, both the class/spec placeholder and the free-text note
/// are optional and independent of each other — a slot can hold just a note ("Bob, still deciding"),
/// just a class/spec, both, or (once cleared down to nothing) go back to having no row at all.
/// Surrogate PK rather than a composite one, since <see cref="WowClassId"/>/<see cref="SpecId"/>/
/// <see cref="Note"/> are all independently nullable and easiest to upsert by row identity.
/// </summary>
[Table("RaidCompositionPreviewSlots")]
public class RaidCompositionPreviewSlot
{
    /// <summary>Surrogate primary key.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>FK to the preview this slot belongs to.</summary>
    public int RaidCompositionPreviewId { get; set; }

    /// <summary>1-based group number within the preview's grid.</summary>
    public int GroupNumber { get; set; }

    /// <summary>1-based slot number within the group.</summary>
    public int SlotNumber { get; set; }

    /// <summary>FK to the placeholder class for this slot, or <c>null</c> if unset.</summary>
    public int? WowClassId { get; set; }

    /// <summary>
    /// FK to the placeholder spec for this slot, or <c>null</c> if unset. When set, always belongs
    /// to <see cref="WowClassId"/> (validated by the command handler, not a DB constraint).
    /// </summary>
    public int? SpecId { get; set; }

    /// <summary>Free-text note (e.g. who's expected to play this slot) — unrestricted, or <c>null</c> if unset.</summary>
    [MaxLength(200)]
    public string? Note { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The preview this slot belongs to.</summary>
    public virtual RaidCompositionPreview RaidCompositionPreview { get; set; } = null!;

    /// <summary>The placeholder class for this slot, or <c>null</c> if unset.</summary>
    public virtual WowClass? WowClass { get; set; }

    /// <summary>The placeholder spec for this slot, or <c>null</c> if unset.</summary>
    public virtual Spec? Spec { get; set; }
}
