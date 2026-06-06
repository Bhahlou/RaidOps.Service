using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Discord;

namespace RaidOps.IntegrationTests.Infrastructure;

/// <summary>
/// Factories for building domain entities to seed the integration test database.
/// All defaults produce valid, minimal data. Callers can override individual properties.
/// </summary>
public static class TestDataBuilder
{
    /// <summary>
    /// Creates a <see cref="User"/> with the given Discord ID.
    /// Use distinct IDs across tests to avoid primary-key conflicts within a shared container.
    /// </summary>
    public static User CreateUser(
        string discordId = "100000000000000001",
        string name = "TestUser") => new()
    {
        DiscordId = discordId,
        Name = name,
        RefreshToken = "test-discord-refresh-token",
        LastRefresh = DateTimeOffset.UtcNow,
    };

    /// <summary>
    /// Creates a <see cref="Guild"/> with the given Discord snowflake ID.
    /// </summary>
    public static Guild CreateGuild(
        string id = "200000000000000001",
        string name = "Test Guild",
        bool isRegistered = false) => new()
    {
        Id = id,
        Name = name,
        IsRegistered = isRegistered,
    };

    /// <summary>
    /// Creates a <see cref="UserGuild"/> linking a user to a guild.
    /// </summary>
    public static UserGuild CreateUserGuild(
        string userDiscordId,
        string guildId,
        bool isAdmin = false) => new()
    {
        UserDiscordId = userDiscordId,
        GuildId = guildId,
        IsAdmin = isAdmin,
    };

    /// <summary>
    /// Creates a <see cref="Realm"/> for the given branch.
    /// BranchId=1 is Retail (always seeded in DB).
    /// </summary>
    public static Realm CreateRealm(
        int branchId = 1,
        string slug = "argent-dawn",
        string name = "Argent Dawn",
        string region = "eu") => new()
    {
        Slug = slug,
        Name = name,
        Region = region,
        BranchId = branchId,
    };

    /// <summary>
    /// Creates a <see cref="Character"/> owned by the given Discord user.
    /// RaceId=1 (Human) and ClassId=8 (Mage) are always seeded in the DB.
    /// BranchId=1 is Retail.
    /// </summary>
    public static Character CreateCharacter(
        string userDiscordId,
        int realmId,
        int branchId = 1,
        int raceId = 1,
        int classId = 8,
        bool isActive = false,
        string name = "TestMage",
        long bnetCharacterId = 99001) => new()
    {
        Name = name,
        BnetCharacterId = bnetCharacterId,
        UserDiscordId = userDiscordId,
        BranchId = branchId,
        RealmId = realmId,
        RaceId = raceId,
        ClassId = classId,
        Faction = Faction.Alliance,
        Gender = Gender.Male,
        IsActiveInRaidOps = isActive,
    };

    /// <summary>
    /// Creates a <see cref="BattleNetAccount"/> linked to the given Discord user.
    /// </summary>
    public static BattleNetAccount CreateBnetAccount(
        string userDiscordId,
        string battleTag = "TestUser#1234") => new()
    {
        UserDiscordId = userDiscordId,
        BnetId = "987654321",
        BattleTag = battleTag,
        AccessToken = "test-bnet-access-token",
        Region = "eu",
        TokenExpiry = DateTimeOffset.UtcNow.AddHours(24),
    };
}
