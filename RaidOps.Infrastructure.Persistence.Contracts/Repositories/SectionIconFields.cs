using RaidOps.Domain.Enums;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>Bundles a section header's icon-related fields — kept together purely to keep <see cref="IGuildAttributionDefinitionsRepository.SetSectionIconAsync"/>'s parameter count reasonable.</summary>
public record SectionIconFields(AttributionIconSource IconSource, int? SpellId, RaidMarkerIcon? RaidMarker, SpecRole? StaticRole);
