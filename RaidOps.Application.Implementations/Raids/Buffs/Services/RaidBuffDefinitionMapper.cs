using RaidOps.Application.Contracts.Raids.Buffs;
using RaidOps.Application.Contracts.Raids.Buffs.Responses;
using RaidOps.Domain.Models.Reference;

namespace RaidOps.Application.Implementations.Raids.Buffs.Services;

/// <summary>Maps raid buff definitions between their write shape, their entity and their response.</summary>
public static class RaidBuffDefinitionMapper
{
    /// <summary>
    /// Builds a new, unattached <see cref="RaidBuffDefinition"/> from validated input: labels are trimmed
    /// and the group/pool keys are trimmed, lower-cased and nulled when blank, so "Armor " and "armor" can't
    /// silently form two groups.
    /// </summary>
    public static RaidBuffDefinition ToEntity(RaidBuffDefinitionInput input) => new()
    {
        SpellId = input.SpellId,
        Scope = input.Scope,
        Kind = input.Kind,
        LabelEn = input.LabelEn.Trim(),
        LabelFr = input.LabelFr.Trim(),
        LabelDe = input.LabelDe.Trim(),
        ExclusiveGroupKey = NormalizeKey(input.ExclusiveGroupKey),
        CapacityPoolKey = NormalizeKey(input.CapacityPoolKey),
        SortOrder = input.SortOrder,
        Sources = input.Sources.Select(s => new RaidBuffSource { ClassId = s.ClassId, SpecId = s.SpecId }).ToList(),
    };

    /// <summary>
    /// Builds the response for a stored definition. The spell's name/icon comes from the availability row
    /// loaded for the definition's own expansion, and is <c>null</c> when the spell has none there.
    /// </summary>
    public static RaidBuffDefinitionResponse ToResponse(RaidBuffDefinition definition)
    {
        var availability = definition.Spell.Availabilities.FirstOrDefault(a => a.ExpansionId == definition.ExpansionId);

        return new RaidBuffDefinitionResponse
        {
            Id = definition.Id,
            ExpansionId = definition.ExpansionId,
            SpellId = definition.SpellId,
            Scope = definition.Scope,
            Kind = definition.Kind,
            LabelEn = definition.LabelEn,
            LabelFr = definition.LabelFr,
            LabelDe = definition.LabelDe,
            ExclusiveGroupKey = definition.ExclusiveGroupKey,
            CapacityPoolKey = definition.CapacityPoolKey,
            SortOrder = definition.SortOrder,
            Sources = definition.Sources.Select(s => new RaidBuffSourceDto { ClassId = s.ClassId, SpecId = s.SpecId }).ToList(),
            Spell = availability == null ? null : new RaidBuffSpellResponse
            {
                NameEn = availability.NameEn,
                NameFr = availability.NameFr,
                NameDe = availability.NameDe,
                IconUrl = availability.IconUrl,
            },
        };
    }

    private static string? NormalizeKey(string? key)
        => string.IsNullOrWhiteSpace(key) ? null : key.Trim().ToLowerInvariant();
}
