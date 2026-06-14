namespace RaidOps.Domain.Enums;

/// <summary>
/// In-guild rank assigned to a player (Discord account) within a RaidOps guild.
/// Drives permission checks (e.g. who can manage settings, kick members, build raids).
/// </summary>
public enum GuildPlayerRank
{
    /// <summary>Casual member — limited visibility, no management rights.</summary>
    Social = 1,

    /// <summary>Regular raid attendee.</summary>
    Casual = 2,

    /// <summary>Core raider.</summary>
    Raider = 3,

    /// <summary>Officer — can manage roster and raid events.</summary>
    Officer = 4,

    /// <summary>Guild master — full management rights.</summary>
    GuildMaster = 5,
}
