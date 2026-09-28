using FluentAssertions;
using RaidOps.Application.Contracts.Raids.Buffs;
using RaidOps.Application.Implementations.Raids.Buffs.Services;
using RaidOps.Domain.Enums;

namespace RaidOps.UnitTests.Application.Raids.Buffs.Services;

/// <summary>Unit tests for <see cref="RaidBuffDefinitionValidator"/>.</summary>
public class RaidBuffDefinitionValidatorTests
{
    private const int SpellId = 16176;
    private const int ClassId = 7;
    private const int SpecId = 264;
    private const int OtherClassId = 5;

    private static readonly IReadOnlySet<int> ClassIds = new HashSet<int> { ClassId, OtherClassId };
    private static readonly IReadOnlyDictionary<int, int> SpecClassIdBySpecId = new Dictionary<int, int> { [SpecId] = ClassId };
    private static readonly IReadOnlySet<int> KnownSpellIds = new HashSet<int> { SpellId };

    private static RaidBuffDefinitionInput MakeValidDefinition(int spellId = SpellId) => new()
    {
        SpellId = spellId,
        Scope = RaidBuffScope.Raid,
        Kind = RaidBuffKind.Buff,
        LabelEn = "+25% armor",
        LabelFr = "+25 % d'armure",
        LabelDe = "+25 % Rüstung",
        SortOrder = 10,
        Sources = [new RaidBuffSourceDto { ClassId = ClassId, SpecId = SpecId }],
    };

    private static List<string> Validate(params RaidBuffDefinitionInput[] definitions)
        => RaidBuffDefinitionValidator.Validate(definitions, ClassIds, SpecClassIdBySpecId, KnownSpellIds);

    [Fact]
    public void Validate_ValidDefinition_ReturnsNoErrors()
    {
        Validate(MakeValidDefinition()).Should().BeEmpty();
    }

    [Fact]
    public void Validate_TooManyDefinitions_ReturnsASingleErrorWithoutInspectingThem()
    {
        var definitions = Enumerable.Range(1, RaidBuffDefinitionValidator.MaxDefinitions + 1)
            .Select(i => MakeValidDefinition(i)) // most are unknown spells, but that must never be reached
            .ToArray();

        var errors = Validate(definitions);

        errors.Should().ContainSingle().Which.Should().Contain(RaidBuffDefinitionValidator.MaxDefinitions.ToString());
    }

    [Fact]
    public void Validate_DuplicateSpellAcrossDefinitions_ReturnsAnError()
    {
        var errors = Validate(MakeValidDefinition(), MakeValidDefinition());

        errors.Should().Contain(e => e.Contains("appears more than once"));
    }

    [Fact]
    public void Validate_UnknownSpell_ReturnsAnError()
    {
        var definition = MakeValidDefinition(spellId: 999999);

        Validate(definition).Should().Contain(e => e.Contains("is not known on this expansion"));
    }

    [Theory]
    [InlineData((RaidBuffScope)99)]
    public void Validate_OutOfRangeScope_ReturnsAnError(RaidBuffScope scope)
    {
        var definition = MakeValidDefinition();
        definition.Scope = scope;

        Validate(definition).Should().Contain(e => e.Contains("scope is not valid"));
    }

    [Theory]
    [InlineData((RaidBuffKind)99)]
    public void Validate_OutOfRangeKind_ReturnsAnError(RaidBuffKind kind)
    {
        var definition = MakeValidDefinition();
        definition.Kind = kind;

        Validate(definition).Should().Contain(e => e.Contains("kind is not valid"));
    }

    [Theory]
    [InlineData(RaidBuffDefinitionValidator.MaxSortOrder + 1)]
    [InlineData(-RaidBuffDefinitionValidator.MaxSortOrder - 1)]
    public void Validate_SortOrderOutOfBounds_ReturnsAnError(int sortOrder)
    {
        var definition = MakeValidDefinition();
        definition.SortOrder = sortOrder;

        Validate(definition).Should().Contain(e => e.Contains("sort order"));
    }

    [Theory]
    [InlineData(RaidBuffDefinitionValidator.MaxSortOrder)]
    [InlineData(-RaidBuffDefinitionValidator.MaxSortOrder)]
    public void Validate_SortOrderAtTheBoundary_IsAccepted(int sortOrder)
    {
        var definition = MakeValidDefinition();
        definition.SortOrder = sortOrder;

        Validate(definition).Should().BeEmpty();
    }

    // ── Labels ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(nameof(RaidBuffDefinitionInput.LabelEn))]
    [InlineData(nameof(RaidBuffDefinitionInput.LabelFr))]
    [InlineData(nameof(RaidBuffDefinitionInput.LabelDe))]
    public void Validate_BlankLabel_ReturnsAnError(string property)
    {
        var definition = MakeValidDefinition();
        SetLabel(definition, property, "   ");

        Validate(definition).Should().Contain(e => e.Contains("label is required"));
    }

    [Theory]
    [InlineData(nameof(RaidBuffDefinitionInput.LabelEn))]
    [InlineData(nameof(RaidBuffDefinitionInput.LabelFr))]
    [InlineData(nameof(RaidBuffDefinitionInput.LabelDe))]
    public void Validate_LabelTooLong_ReturnsAnError(string property)
    {
        var definition = MakeValidDefinition();
        SetLabel(definition, property, new string('a', RaidBuffDefinitionValidator.MaxLabelLength + 1));

        Validate(definition).Should().Contain(e => e.Contains("longer than"));
    }

    [Fact]
    public void Validate_LabelAtTheMaxLength_IsAccepted()
    {
        var definition = MakeValidDefinition();
        definition.LabelEn = new string('a', RaidBuffDefinitionValidator.MaxLabelLength);

        Validate(definition).Should().BeEmpty();
    }

    [Theory]
    [InlineData(nameof(RaidBuffDefinitionInput.LabelEn))]
    [InlineData(nameof(RaidBuffDefinitionInput.LabelFr))]
    [InlineData(nameof(RaidBuffDefinitionInput.LabelDe))]
    public void Validate_LabelWithControlCharacter_ReturnsAnError(string property)
    {
        var definition = MakeValidDefinition();
        SetLabel(definition, property, "bad\u0007label");

        Validate(definition).Should().Contain(e => e.Contains("control characters"));
    }

    private static void SetLabel(RaidBuffDefinitionInput definition, string property, string value)
    {
        switch (property)
        {
            case nameof(RaidBuffDefinitionInput.LabelEn): definition.LabelEn = value; break;
            case nameof(RaidBuffDefinitionInput.LabelFr): definition.LabelFr = value; break;
            case nameof(RaidBuffDefinitionInput.LabelDe): definition.LabelDe = value; break;
        }
    }

    // ── Keys ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankOrNullKeys_AreAccepted(string? key)
    {
        var definition = MakeValidDefinition();
        definition.ExclusiveGroupKey = key;
        definition.CapacityPoolKey = key;

        Validate(definition).Should().BeEmpty();
    }

    [Fact]
    public void Validate_KeyTooLong_ReturnsAnError()
    {
        var definition = MakeValidDefinition();
        definition.ExclusiveGroupKey = new string('a', RaidBuffDefinitionValidator.MaxKeyLength + 1);

        Validate(definition).Should().Contain(e => e.Contains("longer than"));
    }

    [Theory]
    [InlineData("Armor Pct")] // spaces
    [InlineData("armor_pct")] // underscore
    [InlineData("armor pct!")] // punctuation
    public void Validate_KeyWithDisallowedCharacters_ReturnsAnError(string key)
    {
        var definition = MakeValidDefinition();
        definition.CapacityPoolKey = key;

        Validate(definition).Should().Contain(e => e.Contains("letters, digits and hyphens"));
    }

    [Fact]
    public void Validate_KeyUppercaseWithHyphensAndDigits_IsAccepted()
    {
        // The mapper lower-cases keys before storage — validation must judge the normalized form, not the raw input.
        var definition = MakeValidDefinition();
        definition.ExclusiveGroupKey = "Armor-PCT-2";

        Validate(definition).Should().BeEmpty();
    }

    // ── Sources ──────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_NoSources_ReturnsAnError()
    {
        var definition = MakeValidDefinition();
        definition.Sources = [];

        Validate(definition).Should().Contain(e => e.Contains("at least one source"));
    }

    [Fact]
    public void Validate_TooManySources_ReturnsAnError()
    {
        var definition = MakeValidDefinition();
        definition.Sources = Enumerable.Range(0, RaidBuffDefinitionValidator.MaxSourcesPerDefinition + 1)
            .Select(_ => new RaidBuffSourceDto { ClassId = ClassId, SpecId = null })
            .ToList();

        Validate(definition).Should().Contain(e => e.Contains("at most"));
    }

    [Fact]
    public void Validate_UnknownClass_ReturnsAnError()
    {
        var definition = MakeValidDefinition();
        definition.Sources = [new RaidBuffSourceDto { ClassId = 999, SpecId = null }];

        Validate(definition).Should().Contain(e => e.Contains("class 999 does not exist"));
    }

    [Fact]
    public void Validate_AnySpecSourceOnAKnownClass_IsAccepted()
    {
        var definition = MakeValidDefinition();
        definition.Sources = [new RaidBuffSourceDto { ClassId = ClassId, SpecId = null }];

        Validate(definition).Should().BeEmpty();
    }

    [Fact]
    public void Validate_UnknownSpec_ReturnsAnError()
    {
        var definition = MakeValidDefinition();
        definition.Sources = [new RaidBuffSourceDto { ClassId = ClassId, SpecId = 999 }];

        Validate(definition).Should().Contain(e => e.Contains("does not exist or does not belong"));
    }

    [Fact]
    public void Validate_SpecBelongingToADifferentClassThanTheSource_ReturnsAnError()
    {
        // SpecId is a real, known spec, but it belongs to ClassId, not OtherClassId.
        var definition = MakeValidDefinition();
        definition.Sources = [new RaidBuffSourceDto { ClassId = OtherClassId, SpecId = SpecId }];

        Validate(definition).Should().Contain(e => e.Contains("does not exist or does not belong"));
    }

    [Fact]
    public void Validate_DuplicateSourceWithinTheSameDefinition_ReturnsAnError()
    {
        var definition = MakeValidDefinition();
        definition.Sources = [new RaidBuffSourceDto { ClassId = ClassId, SpecId = SpecId }, new RaidBuffSourceDto { ClassId = ClassId, SpecId = SpecId }];

        Validate(definition).Should().Contain(e => e.Contains("listed more than once"));
    }

    [Fact]
    public void Validate_SameClassWithDifferentSpecs_IsNotADuplicate()
    {
        var definition = MakeValidDefinition();
        definition.Sources = [new RaidBuffSourceDto { ClassId = ClassId, SpecId = SpecId }, new RaidBuffSourceDto { ClassId = ClassId, SpecId = null }];

        Validate(definition).Should().BeEmpty();
    }

    [Fact]
    public void Validate_MultipleProblemsOnOneDefinition_ReturnsAllOfThem()
    {
        var definition = MakeValidDefinition();
        definition.LabelEn = "";
        definition.Sources = [];

        var errors = Validate(definition);

        errors.Should().Contain(e => e.Contains("label is required")).And.Contain(e => e.Contains("at least one source"));
    }
}
