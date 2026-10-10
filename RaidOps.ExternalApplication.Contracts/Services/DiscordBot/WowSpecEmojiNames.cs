namespace RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

/// <summary>
/// Stable application-emoji name for a spec icon, e.g. <c>"spec_warrior_fury"</c>. Class-prefixed
/// rather than just the spec name (<c>"spec_fury"</c>) because spec names aren't unique across
/// classes — "Holy" is both Priest and Paladin, "Restoration" is both Druid and Shaman,
/// "Protection" is both Warrior and Paladin — but (class, spec name) always is. Shared between the
/// emoji sync manifest (built from <c>GetSpecsQuery</c> results) and any caller resolving
/// <see cref="IEmojiService.GetMarkdown"/> for a spec, so both sides always agree on the name.
/// </summary>
public static class WowSpecEmojiNames
{
    /// <summary>
    /// <paramref name="classId"/> resolves to a slug via <see cref="WowClassEmojiNames"/> (falls
    /// back to the raw numeric ID for an unrecognized class — never throws, a sync should degrade
    /// rather than fail outright over one bad row). <paramref name="specName"/> is lowercased with
    /// everything but letters/digits stripped, so e.g. "Beast Mastery" becomes "beastmastery".
    /// </summary>
    public static string GetName(int classId, string specName)
    {
        var classSlug = WowClassEmojiNames.ByClassId.TryGetValue(classId, out var className)
            ? className["class_".Length..]
            : classId.ToString();

        return $"spec_{classSlug}_{Slugify(specName)}";
    }

    private static string Slugify(string value) =>
        new([.. value.ToLowerInvariant().Where(char.IsLetterOrDigit)]);
}
