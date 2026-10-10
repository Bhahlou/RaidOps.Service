namespace RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

/// <summary>
/// Stable application-emoji names for each WoW class icon, keyed by Blizzard's class ID (1-13,
/// permanently fixed across every version of the game — never reused or renumbered). Shared
/// between the emoji sync manifest (which also needs the source image URL, see
/// <c>ApplicationEmojiManifest</c> in RaidOps.ExternalApplication.Implementations) and any caller
/// building an <see cref="IEmojiService.GetMarkdown"/> lookup, so both sides always agree on the
/// name without duplicating the string literal.
/// </summary>
public static class WowClassEmojiNames
{
    /// <summary>Emoji name (e.g. <c>"class_warrior"</c>) keyed by Blizzard class ID.</summary>
    public static readonly IReadOnlyDictionary<int, string> ByClassId = new Dictionary<int, string>
    {
        [1] = "class_warrior",
        [2] = "class_paladin",
        [3] = "class_hunter",
        [4] = "class_rogue",
        [5] = "class_priest",
        [6] = "class_deathknight",
        [7] = "class_shaman",
        [8] = "class_mage",
        [9] = "class_warlock",
        [10] = "class_monk",
        [11] = "class_druid",
        [12] = "class_demonhunter",
        [13] = "class_evoker",
    };
}
