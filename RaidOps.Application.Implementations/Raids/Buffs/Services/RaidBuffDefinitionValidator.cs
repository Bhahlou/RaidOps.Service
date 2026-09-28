using RaidOps.Application.Contracts.Raids.Buffs;
using RaidOps.Domain.Enums;

namespace RaidOps.Application.Implementations.Raids.Buffs.Services;

/// <summary>
/// Pure validation of raid buff definitions before they're written, shared by the admin screen's
/// single-row save and the JSON import. Returns every problem found rather than the first, so an
/// import of a whole list can be fixed in one pass.
/// </summary>
public static class RaidBuffDefinitionValidator
{
    /// <summary>Maximum length of an effect label — matches the column size.</summary>
    public const int MaxLabelLength = 128;

    /// <summary>Maximum length of an exclusive-group or capacity-pool key — matches the column size.</summary>
    public const int MaxKeyLength = 64;

    /// <summary>Most definitions one request may carry — far above a real list, so a pasted payload can't be used to flood the database.</summary>
    public const int MaxDefinitions = 500;

    /// <summary>Most sources one definition may list — there are only a dozen classes.</summary>
    public const int MaxSourcesPerDefinition = 20;

    /// <summary>Largest absolute sort order accepted.</summary>
    public const int MaxSortOrder = 1_000_000;

    /// <summary>
    /// Validates <paramref name="definitions"/> against the reference data they must point at.
    /// </summary>
    /// <param name="definitions">The definitions to check.</param>
    /// <param name="specClassIdBySpecId">Every known spec ID mapped to the ID of the class it belongs to; its keys are also the set of valid spec IDs.</param>
    /// <param name="classIds">Every known class ID.</param>
    /// <param name="knownSpellIds">The subset of the definitions' spell IDs that exist on the target expansion.</param>
    /// <returns>One human-readable message per problem; empty when everything is valid.</returns>
    public static List<string> Validate(
        IReadOnlyList<RaidBuffDefinitionInput> definitions,
        IReadOnlySet<int> classIds,
        IReadOnlyDictionary<int, int> specClassIdBySpecId,
        IReadOnlySet<int> knownSpellIds)
    {
        if (definitions.Count > MaxDefinitions)
            return [$"At most {MaxDefinitions} definitions can be written at once."];

        var errors = new List<string>();

        foreach (var duplicate in definitions.GroupBy(d => d.SpellId).Where(g => g.Count() > 1))
            errors.Add($"Spell {duplicate.Key} appears more than once.");

        foreach (var definition in definitions)
        {
            if (!knownSpellIds.Contains(definition.SpellId))
                errors.Add($"Spell {definition.SpellId} is not known on this expansion.");

            ValidateEnums(definition, errors);
            ValidateText(definition, errors);
            ValidateSources(definition, classIds, specClassIdBySpecId, errors);
        }

        return errors;
    }

    private static void ValidateEnums(RaidBuffDefinitionInput definition, List<string> errors)
    {
        // JSON enum binding also accepts raw numbers, so an out-of-range value must be refused here.
        if (!Enum.IsDefined(definition.Scope))
            errors.Add($"Spell {definition.SpellId}: the scope is not valid.");

        if (!Enum.IsDefined(definition.Kind))
            errors.Add($"Spell {definition.SpellId}: the kind is not valid.");

        if (Math.Abs((long)definition.SortOrder) > MaxSortOrder)
            errors.Add($"Spell {definition.SpellId}: the sort order must be between -{MaxSortOrder} and {MaxSortOrder}.");
    }

    private static void ValidateText(RaidBuffDefinitionInput definition, List<string> errors)
    {
        var spell = definition.SpellId;

        foreach (var (locale, label) in new[] { ("en", definition.LabelEn), ("fr", definition.LabelFr), ("de", definition.LabelDe) })
        {
            if (string.IsNullOrWhiteSpace(label))
                errors.Add($"Spell {spell}: the {locale} label is required.");
            else if (label.Trim().Length > MaxLabelLength)
                errors.Add($"Spell {spell}: the {locale} label is longer than {MaxLabelLength} characters.");
            else if (label.Any(char.IsControl))
                errors.Add($"Spell {spell}: the {locale} label contains control characters.");
        }

        ValidateKey(spell, "exclusive group", definition.ExclusiveGroupKey, errors);
        ValidateKey(spell, "capacity pool", definition.CapacityPoolKey, errors);
    }

    /// <summary>A key, once trimmed and lower-cased (as the mapper stores it), may only hold a-z, 0-9 and hyphens — it is a slug, never free text.</summary>
    private static void ValidateKey(int spell, string name, string? key, List<string> errors)
    {
        var normalized = key?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalized))
            return;

        if (normalized.Length > MaxKeyLength)
            errors.Add($"Spell {spell}: the {name} key is longer than {MaxKeyLength} characters.");
        else if (!normalized.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-'))
            errors.Add($"Spell {spell}: the {name} key may only contain letters, digits and hyphens.");
    }

    private static void ValidateSources(
        RaidBuffDefinitionInput definition,
        IReadOnlySet<int> classIds,
        IReadOnlyDictionary<int, int> specClassIdBySpecId,
        List<string> errors)
    {
        var spell = definition.SpellId;

        if (definition.Sources.Count == 0)
            errors.Add($"Spell {spell}: at least one source class is required.");
        else if (definition.Sources.Count > MaxSourcesPerDefinition)
            errors.Add($"Spell {spell}: at most {MaxSourcesPerDefinition} sources are allowed.");

        foreach (var source in definition.Sources)
        {
            if (!classIds.Contains(source.ClassId))
                errors.Add($"Spell {spell}: class {source.ClassId} does not exist.");
            else if (source.SpecId is { } specId && (!specClassIdBySpecId.TryGetValue(specId, out var specClassId) || specClassId != source.ClassId))
                errors.Add($"Spell {spell}: spec {specId} does not exist or does not belong to class {source.ClassId}.");
        }

        if (definition.Sources.GroupBy(s => (s.ClassId, s.SpecId)).Any(g => g.Count() > 1))
            errors.Add($"Spell {spell}: the same source is listed more than once.");
    }
}
