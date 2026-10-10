namespace RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

/// <summary>
/// The expansion each WoW class first became playable, keyed by Blizzard class ID — mirrors
/// <c>RaidOpsDbContext.SeedClasses</c>' <c>WowClass.FirstExpansionId</c> seed values (Expansion IDs
/// are chronological/<c>ReleaseOrder</c>-equivalent, see <c>RaidOpsDbContext.SeedExpansions</c>).
/// Lets the signup-call embed show only the classes actually available on a raid's branch (e.g. no
/// Monk/Death Knight/Demon Hunter/Evoker columns on a Classic Era branch) without a DB round trip
/// for what's permanently-fixed reference data, same rationale as <see cref="WowClassNames"/>.
/// </summary>
public static class WowClassAvailability
{
    /// <summary>Blizzard class ID → the expansion ID it first became playable in.</summary>
    public static readonly IReadOnlyDictionary<int, int> FirstExpansionIdByClassId = new Dictionary<int, int>
    {
        [1] = 1,   // Warrior — Classic
        [2] = 1,   // Paladin — Classic
        [3] = 1,   // Hunter — Classic
        [4] = 1,   // Rogue — Classic
        [5] = 1,   // Priest — Classic
        [6] = 3,   // Death Knight — Wrath of the Lich King
        [7] = 1,   // Shaman — Classic
        [8] = 1,   // Mage — Classic
        [9] = 1,   // Warlock — Classic
        [10] = 5,  // Monk — Mists of Pandaria
        [11] = 1,  // Druid — Classic
        [12] = 7,  // Demon Hunter — Legion
        [13] = 10, // Evoker — Dragonflight
    };
}
