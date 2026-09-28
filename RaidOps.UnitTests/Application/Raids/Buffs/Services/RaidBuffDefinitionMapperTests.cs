using FluentAssertions;
using RaidOps.Application.Contracts.Raids.Buffs;
using RaidOps.Application.Implementations.Raids.Buffs.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Reference;

namespace RaidOps.UnitTests.Application.Raids.Buffs.Services;

/// <summary>Unit tests for <see cref="RaidBuffDefinitionMapper"/>.</summary>
public class RaidBuffDefinitionMapperTests
{
    private static RaidBuffDefinitionInput MakeInput() => new()
    {
        SpellId = 16176,
        Scope = RaidBuffScope.Raid,
        Kind = RaidBuffKind.Buff,
        LabelEn = "  +25% armor  ",
        LabelFr = "+25 % d'armure",
        LabelDe = "+25 % Rüstung",
        ExclusiveGroupKey = null,
        CapacityPoolKey = null,
        SortOrder = 10,
        Sources = [new RaidBuffSourceDto { ClassId = 7, SpecId = 264 }, new RaidBuffSourceDto { ClassId = 5, SpecId = null }],
    };

    // ── ToEntity ─────────────────────────────────────────────────────────────

    [Fact]
    public void ToEntity_CopiesScalarFieldsAndTrimsLabels()
    {
        var entity = RaidBuffDefinitionMapper.ToEntity(MakeInput());

        entity.SpellId.Should().Be(16176);
        entity.Scope.Should().Be(RaidBuffScope.Raid);
        entity.Kind.Should().Be(RaidBuffKind.Buff);
        entity.LabelEn.Should().Be("+25% armor");
        entity.LabelFr.Should().Be("+25 % d'armure");
        entity.LabelDe.Should().Be("+25 % Rüstung");
        entity.SortOrder.Should().Be(10);
    }

    [Fact]
    public void ToEntity_MapsEverySourceOneToOne()
    {
        var entity = RaidBuffDefinitionMapper.ToEntity(MakeInput());

        entity.Sources.Should().SatisfyRespectively(
            s => { s.ClassId.Should().Be(7); s.SpecId.Should().Be(264); },
            s => { s.ClassId.Should().Be(5); s.SpecId.Should().BeNull(); });
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("Armor-PCT", "armor-pct")]
    [InlineData("  armor-pct  ", "armor-pct")]
    public void ToEntity_NormalizesTheExclusiveGroupKey(string? input, string? expected)
    {
        var definition = MakeInput();
        definition.ExclusiveGroupKey = input;

        RaidBuffDefinitionMapper.ToEntity(definition).ExclusiveGroupKey.Should().Be(expected);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("Paladin-Blessings", "paladin-blessings")]
    public void ToEntity_NormalizesTheCapacityPoolKey(string? input, string? expected)
    {
        var definition = MakeInput();
        definition.CapacityPoolKey = input;

        RaidBuffDefinitionMapper.ToEntity(definition).CapacityPoolKey.Should().Be(expected);
    }

    // ── ToResponse ───────────────────────────────────────────────────────────

    private static RaidBuffDefinition MakeEntity(IEnumerable<SpellAvailability>? availabilities = null) => new()
    {
        Id = 1,
        ExpansionId = 12,
        SpellId = 16176,
        Scope = RaidBuffScope.Raid,
        Kind = RaidBuffKind.Buff,
        LabelEn = "+25% armor",
        LabelFr = "+25 % d'armure",
        LabelDe = "+25 % Rüstung",
        ExclusiveGroupKey = "armor-pct",
        CapacityPoolKey = null,
        SortOrder = 10,
        Sources = [new RaidBuffSource { ClassId = 7, SpecId = 264 }],
        Spell = new Spell { Id = 16176, Availabilities = (availabilities ?? []).ToList() },
    };

    [Fact]
    public void ToResponse_CopiesEveryScalarFieldAndSource()
    {
        var response = RaidBuffDefinitionMapper.ToResponse(MakeEntity());

        response.Id.Should().Be(1);
        response.ExpansionId.Should().Be(12);
        response.SpellId.Should().Be(16176);
        response.Scope.Should().Be(RaidBuffScope.Raid);
        response.Kind.Should().Be(RaidBuffKind.Buff);
        response.LabelEn.Should().Be("+25% armor");
        response.ExclusiveGroupKey.Should().Be("armor-pct");
        response.CapacityPoolKey.Should().BeNull();
        response.SortOrder.Should().Be(10);
        response.Sources.Should().ContainSingle(s => s.ClassId == 7 && s.SpecId == 264);
    }

    [Fact]
    public void ToResponse_SpellHasAnAvailabilityOnTheDefinitionsExpansion_ResolvesItsNameAndIcon()
    {
        var entity = MakeEntity(
        [
            new SpellAvailability { SpellId = 16176, ExpansionId = 2, NameEn = "Other Expansion Name", NameFr = "x", NameDe = "x", IconUrl = "https://cdn/other.jpg" },
            new SpellAvailability { SpellId = 16176, ExpansionId = 12, NameEn = "Ancestral Healing", NameFr = "Guérison des anciens", NameDe = "Heilung der Ahnen", IconUrl = "https://cdn/ancestral.jpg" },
        ]);

        var response = RaidBuffDefinitionMapper.ToResponse(entity);

        response.Spell.Should().NotBeNull();
        response.Spell!.NameEn.Should().Be("Ancestral Healing");
        response.Spell.NameFr.Should().Be("Guérison des anciens");
        response.Spell.NameDe.Should().Be("Heilung der Ahnen");
        response.Spell.IconUrl.Should().Be("https://cdn/ancestral.jpg");
    }

    [Fact]
    public void ToResponse_SpellHasNoAvailabilityOnTheDefinitionsExpansion_ReturnsNullSpell()
    {
        var entity = MakeEntity([new SpellAvailability { SpellId = 16176, ExpansionId = 2, NameEn = "x", NameFr = "x", NameDe = "x", IconUrl = "x" }]);

        RaidBuffDefinitionMapper.ToResponse(entity).Spell.Should().BeNull();
    }
}
