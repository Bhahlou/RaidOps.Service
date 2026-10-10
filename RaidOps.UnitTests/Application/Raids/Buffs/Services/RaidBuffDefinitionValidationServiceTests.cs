using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Raids.Buffs;
using RaidOps.Application.Implementations.Raids.Buffs.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Buffs.Services;

/// <summary>
/// Unit tests for <see cref="RaidBuffDefinitionValidationService"/> — proves it loads the right
/// reference data (only the spells actually referenced, not the whole table) and hands it to
/// <see cref="RaidBuffDefinitionValidator"/> correctly. The validation rules themselves are covered
/// by <see cref="RaidBuffDefinitionValidatorTests"/>.
/// </summary>
public class RaidBuffDefinitionValidationServiceTests
{
    private readonly Mock<IWowClassRepository> _classes = new();
    private readonly Mock<ISpecRepository> _specs = new();
    private readonly Mock<ISpellRepository> _spells = new();
    private readonly RaidBuffDefinitionValidationService _sut;

    private const int ExpansionId = 12;

    private static readonly int[] SingleKnownSpellId = [16176];

    public RaidBuffDefinitionValidationServiceTests()
    {
        _classes.Setup(c => c.GetAllAsync(default)).ReturnsAsync([new WowClass { Id = 7, Name = "Shaman", Color = "0070DE" }]);
        _specs.Setup(s => s.GetAllAsync(default)).ReturnsAsync([new Spec { Id = 264, Name = "Restoration", ClassId = 7 }]);
        _sut = new RaidBuffDefinitionValidationService(_classes.Object, _specs.Object, _spells.Object);
    }

    private static RaidBuffDefinitionInput MakeDefinition(int spellId) => new()
    {
        SpellId = spellId,
        Scope = RaidBuffScope.Raid,
        Kind = RaidBuffKind.Buff,
        LabelEn = "en",
        LabelFr = "fr",
        LabelDe = "de",
        SortOrder = 0,
        Sources = [new RaidBuffSourceDto { ClassId = 7, SpecId = 264 }],
    };

    [Fact]
    public async Task ValidateAsync_SpellKnownOnTheExpansion_ReturnsNoErrors()
    {
        _spells.Setup(s => s.GetAvailabilitiesAsync(ExpansionId, It.Is<List<int>>(ids => ids.SequenceEqual(SingleKnownSpellId)), default))
            .ReturnsAsync([new SpellAvailability { SpellId = 16176, ExpansionId = ExpansionId, NameEn = "x", NameFr = "x", NameDe = "x", IconUrl = "x" }]);

        var errors = await _sut.ValidateAsync(ExpansionId, [MakeDefinition(16176)], default);

        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_SpellNotReturnedByTheRepository_ReturnsAnError()
    {
        _spells.Setup(s => s.GetAvailabilitiesAsync(ExpansionId, It.IsAny<List<int>>(), default)).ReturnsAsync([]);

        var errors = await _sut.ValidateAsync(ExpansionId, [MakeDefinition(16176)], default);

        errors.Should().Contain(e => e.Contains("is not known on this expansion"));
    }

    [Fact]
    public async Task ValidateAsync_QueriesEachDistinctSpellIdOnlyOnce()
    {
        _spells.Setup(s => s.GetAvailabilitiesAsync(ExpansionId, It.IsAny<List<int>>(), default)).ReturnsAsync([]);

        await _sut.ValidateAsync(ExpansionId, [MakeDefinition(16176), MakeDefinition(16176), MakeDefinition(14892)], default);

        _spells.Verify(s => s.GetAvailabilitiesAsync(ExpansionId, It.Is<List<int>>(ids => ids.Count == 2 && ids.Contains(16176) && ids.Contains(14892)), default), Times.Once);
    }
}
