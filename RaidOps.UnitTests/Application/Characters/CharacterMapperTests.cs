using FluentAssertions;
using RaidOps.Application.Implementations.Characters;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
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
        dto.Specs.Should().BeEmpty();
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

        dto.Specs.Should().HaveCount(2);
        dto.Specs[0].IsMain.Should().BeTrue();
        dto.Specs[0].SpecId.Should().Be(72);
        dto.Specs[1].IsMain.Should().BeFalse();
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

        var dto = CharacterMapper.ToDto(character).Specs.Single();

        dto.SpecId.Should().Be(71);
        dto.Name.Should().Be("Arms");
        dto.IconUrl.Should().Be("https://cdn/arms.jpg");
        dto.IsMain.Should().BeTrue();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Character MakeCharacter(
        Faction faction = Faction.Alliance,
        ICollection<CharacterExpansionState>? states = null) => new()
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
    };

    private static CharacterSpec MakeSpec(int specId, string name, bool isMain, string iconUrl = "") => new()
    {
        SpecId = specId,
        IsMain = isMain,
        Spec   = new Spec { Id = specId, Name = name, IconUrl = iconUrl },
    };
}
