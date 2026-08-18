namespace RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

/// <summary>
/// Display name for each WoW class, keyed by Blizzard class ID — mirrors <see cref="WowClassEmojiNames"/>
/// (same fixed IDs, same source of truth as <c>RaidOpsDbContext.SeedClasses</c>'s English <c>WowClass.Name</c>
/// seed data). Lets the signup-call embed enumerate every class column even for classes with zero
/// signups, without a DB round trip for what's permanently static reference data.
/// </summary>
public static class WowClassNames
{
    /// <summary>English display name (e.g. <c>"Death Knight"</c>) keyed by Blizzard class ID, in game order.</summary>
    public static readonly IReadOnlyDictionary<int, string> ByClassId = new Dictionary<int, string>
    {
        [1] = "Warrior",
        [2] = "Paladin",
        [3] = "Hunter",
        [4] = "Rogue",
        [5] = "Priest",
        [6] = "Death Knight",
        [7] = "Shaman",
        [8] = "Mage",
        [9] = "Warlock",
        [10] = "Monk",
        [11] = "Druid",
        [12] = "Demon Hunter",
        [13] = "Evoker",
    };
}
