using FluentAssertions;
using RaidOps.Application.Implementations.Characters;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Reference;

namespace RaidOps.UnitTests.Application.Characters;

public class CharacterMapperTests
{
    // ── Active state selection ────────────────────────────────────────────────

    [Fact]
    public void ToDto_ActiveState_UsesActiveLevelAndItemLevel()
    {
        var character = MakeCharacter(states:
        [
            new CharacterExpansionState { ExpansionId = 10, Level = 80, ItemLevel = 600, IsActive = true },
            new CharacterExpansionState { ExpansionId = 9,  Level = 70, ItemLevel = 500, IsActive = false },
        ]);

        var dto = CharacterMapper.ToDto(character);

        dto.Level.Should().Be(80);
        dto.ItemLevel.Should().Be(600);
    }

    [Fact]
    public void ToDto_NoActiveState_FallsBackToHighestLevel()
    {
        var character = MakeCharacter(states:
        [
            new CharacterExpansionState { ExpansionId = 9,  Level = 60, IsActive = false },
            new CharacterExpansionState { ExpansionId = 10, Level = 80, IsActive = false },
        ]);

        var dto = CharacterMapper.ToDto(character);

        dto.Level.Should().Be(80);
    }

    [Fact]
    public void ToDto_NoExpansionStates_ReturnsLevelZeroAndEmptySpecs()
    {
        var character = MakeCharacter(states: []);

        var dto = CharacterMapper.ToDto(character);

        dto.Level.Should().Be(0);
        dto.ItemLevel.Should().BeNull();
        dto.GuildName.Should().BeNull();
        dto.BnetSpecs.Should().BeEmpty();
    }

    // ── Field mapping ─────────────────────────────────────────────────────────

    [Fact]
    public void ToDto_MapsAllScalarFields()
    {
        var character = MakeCharacter();

        var dto = CharacterMapper.ToDto(character);

        dto.Id.Should().Be(character.Id);
        dto.Name.Should().Be("Arthas");
        dto.ClassId.Should().Be(6);
        dto.ClassName.Should().Be("Death Knight");
        dto.RaceId.Should().Be(1);
        dto.RaceName.Should().Be("Human");
        dto.BranchName.Should().Be("Retail");
        dto.RealmName.Should().Be("Kazzak");
        dto.RealmSlug.Should().Be("kazzak");
        dto.AvatarUrl.Should().Be("https://cdn/avatar.jpg");
    }

    [Fact]
    public void ToDto_ClassColor_IsPrefixedWithHash()
    {
        var dto = CharacterMapper.ToDto(MakeCharacter());

        dto.ClassColor.Should().Be("#C41F3B");
    }

    [Fact]
    public void ToDto_Faction_IsUpperCase()
    {
        var alliance = MakeCharacter(faction: Faction.Alliance);
        var horde    = MakeCharacter(faction: Faction.Horde);

        CharacterMapper.ToDto(alliance).Faction.Should().Be("ALLIANCE");
        CharacterMapper.ToDto(horde).Faction.Should().Be("HORDE");
    }

    [Fact]
    public void ToDto_GuildName_MappedFromActiveState()
    {
        var character = MakeCharacter(states:
        [
            new CharacterExpansionState { ExpansionId = 10, Level = 80, GuildName = "RaidOps", IsActive = true },
        ]);

        CharacterMapper.ToDto(character).GuildName.Should().Be("RaidOps");
    }

    // ── Spec ordering ─────────────────────────────────────────────────────────

    [Fact]
    public void ToDto_Specs_MainSpecComesFirst()
    {
        var offspec = MakeSpec(specId: 71, name: "Arms",       isMain: false);
        var main    = MakeSpec(specId: 72, name: "Protection", isMain: true);

        var character = MakeCharacter(states:
        [
            new CharacterExpansionState
            {
                ExpansionId = 10, Level = 80, IsActive = true,
                Specs = [offspec, main],
            },
        ]);

        var dto = CharacterMapper.ToDto(character);

        dto.BnetSpecs.Should().HaveCount(2);
        dto.BnetSpecs[0].IsMain.Should().BeTrue();
        dto.BnetSpecs[0].SpecId.Should().Be(72);
        dto.BnetSpecs[1].IsMain.Should().BeFalse();
    }

    [Fact]
    public void ToDto_Specs_MapsSpecFields()
    {
        var spec = MakeSpec(specId: 71, name: "Arms", isMain: true, iconUrl: "https://cdn/arms.jpg");

        var character = MakeCharacter(states:
        [
            new CharacterExpansionState
            {
                ExpansionId = 10, Level = 80, IsActive = true,
                Specs = [spec],
            },
        ]);

        var dto = CharacterMapper.ToDto(character).BnetSpecs.Single();

        dto.SpecId.Should().Be(71);
        dto.Name.Should().Be("Arms");
        dto.IconUrl.Should().Be("https://cdn/arms.jpg");
        dto.IsMain.Should().BeTrue();
    }

    // ── Raid specs ───────────────────────────────────────────────────────────

    [Fact]
    public void ToDto_RaidSpecs_MainSpecComesFirst()
    {
        var offspec = MakeRaidSpec(specId: 71, name: "Arms",       isMain: false);
        var main    = MakeRaidSpec(specId: 73, name: "Protection", isMain: true);

        var character = MakeCharacter(raidSpecs: [offspec, main]);

        var dto = CharacterMapper.ToDto(character);

        dto.RaidSpecs.Should().HaveCount(2);
        dto.RaidSpecs[0].IsMain.Should().BeTrue();
        dto.RaidSpecs[0].SpecId.Should().Be(73);
        dto.RaidSpecs[1].IsMain.Should().BeFalse();
    }

    [Fact]
    public void ToDto_RaidSpecs_MapsSpecFields()
    {
        var raidSpec = MakeRaidSpec(specId: 71, name: "Arms", isMain: true, iconUrl: "https://cdn/arms.jpg");

        var character = MakeCharacter(raidSpecs: [raidSpec]);

        var dto = CharacterMapper.ToDto(character).RaidSpecs.Single();

        dto.SpecId.Should().Be(71);
        dto.Name.Should().Be("Arms");
        dto.IconUrl.Should().Be("https://cdn/arms.jpg");
        dto.IsMain.Should().BeTrue();
    }

    // ── Guild memberships ────────────────────────────────────────────────────

    [Fact]
    public void ToDto_NoMemberships_ReturnsEmptyList()
    {
        var character = MakeCharacter();

        CharacterMapper.ToDto(character).GuildMemberships.Should().BeEmpty();
    }

    [Fact]
    public void ToDto_GuildMemberships_MapsAllFields()
    {
        var joinedAt = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var membership = MakeMembership(
            guildId: "guild-1", guildName: "RaidOps", guildIconHash: "icon123",
            rank: CharacterRank.Split, joinedAt: joinedAt);

        var character = MakeCharacter(memberships: [membership]);

        var dto = CharacterMapper.ToDto(character).GuildMemberships.Single();

        dto.GuildId.Should().Be("guild-1");
        dto.GuildName.Should().Be("RaidOps");
        dto.GuildIconHash.Should().Be("icon123");
        dto.CharacterRank.Should().Be(CharacterRank.Split);
        dto.JoinedAt.Should().Be(joinedAt);
    }

    [Fact]
    public void ToDto_GuildMemberships_NoCustomIcon_MapsIconHashAsNull()
    {
        var membership = MakeMembership(guildId: "guild-1", guildName: "RaidOps", guildIconHash: null);

        var character = MakeCharacter(memberships: [membership]);

        CharacterMapper.ToDto(character).GuildMemberships.Single().GuildIconHash.Should().BeNull();
    }

    [Fact]
    public void ToDto_GuildMemberships_MultipleEntries_MapsAll()
    {
        var first  = MakeMembership(guildId: "guild-1", guildName: "RaidOps");
        var second = MakeMembership(guildId: "guild-2", guildName: "Other Guild");

        var character = MakeCharacter(memberships: [first, second]);

        var dto = CharacterMapper.ToDto(character);

        dto.GuildMemberships.Should().HaveCount(2);
        dto.GuildMemberships.Select(m => m.GuildId).Should().BeEquivalentTo(["guild-1", "guild-2"]);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Character MakeCharacter(
        Faction faction = Faction.Alliance,
        ICollection<CharacterExpansionState>? states = null,
        ICollection<CharacterRaidSpec>? raidSpecs = null,
        ICollection<GuildMembership>? memberships = null) => new()
    {
        Id            = 1,
        Name          = "Arthas",
        Faction       = faction,
        ClassId       = 6,
        RaceId        = 1,
        AvatarUrl     = "https://cdn/avatar.jpg",
        UserDiscordId = "user-1",
        Class  = new WowClass { Id = 6, Name = "Death Knight", Color = "C41F3B" },
        Race   = new Race     { Id = 1, Name = "Human" },
        Branch = new Branch   { Id = 1, Name = "Retail",  BnetNamespacePrefix = "dynamic", CurrentExpansionId = 10 },
        Realm  = new Realm    { Id = 1, Name = "Kazzak", Slug = "kazzak", Region = "eu", BranchId = 1 },
        ExpansionStates = states ?? [new CharacterExpansionState { ExpansionId = 10, Level = 80, IsActive = true }],
        RaidSpecs = raidSpecs ?? [],
        GuildMemberships = memberships ?? [],
    };

    private static GuildMembership MakeMembership(
        string guildId, string guildName, string? guildIconHash = null,
        CharacterRank rank = CharacterRank.Main, DateTime? joinedAt = null) => new()
    {
        CharacterId   = 1,
        GuildId       = guildId,
        CharacterRank = rank,
        JoinedAt      = joinedAt ?? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        Guild         = new Guild { Id = guildId, Name = guildName, IconHash = guildIconHash },
    };

    private static BnetCharacterSpec MakeSpec(int specId, string name, bool isMain, string iconUrl = "") => new()
    {
        SpecId = specId,
        IsMain = isMain,
        Spec   = new Spec { Id = specId, Name = name, IconUrl = iconUrl },
    };

    private static CharacterRaidSpec MakeRaidSpec(int specId, string name, bool isMain, string iconUrl = "") => new()
    {
        SpecId = specId,
        IsMain = isMain,
        Spec   = new Spec { Id = specId, Name = name, IconUrl = iconUrl },
    };
}
