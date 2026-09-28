using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Raids.Spells.Queries;
using RaidOps.Application.Implementations.Raids.Spells.QueryHandlers;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Spells.QueryHandlers;

/// <summary>
/// Unit tests for <see cref="SearchSpellsQueryHandler"/>. The query is scoped to an expansion only —
/// no guild, branch or access check — since spells are public reference data.
/// </summary>
public class SearchSpellsQueryHandlerTests
{
    private readonly Mock<ISpellRepository> _spells = new();
    private readonly SearchSpellsQueryHandler _sut;

    private const int ExpansionId = 2;

    private static SearchSpellsQuery MakeQuery(string locale = "en") => new()
    {
        ExpansionId = ExpansionId, SearchTerm = "frappe", Locale = locale,
    };

    public SearchSpellsQueryHandlerTests()
    {
        _sut = new SearchSpellsQueryHandler(_spells.Object);
    }

    [Fact]
    public async Task HandleAsync_SearchesWithinTheGivenExpansionAndCapsAt20Results()
    {
        _spells.Setup(s => s.SearchAsync(ExpansionId, "frappe", "en", 20, default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
        _spells.Verify(s => s.SearchAsync(ExpansionId, "frappe", "en", 20, default), Times.Once);
    }

    [Theory]
    [InlineData("fr", "Frappe mortelle (FR)")]
    [InlineData("de", "Frappe mortelle (DE)")]
    [InlineData("en", "Frappe mortelle (EN)")]
    [InlineData("es", "Frappe mortelle (EN)")]
    public async Task HandleAsync_ReturnsNameLocalizedForTheRequesterLocale(string locale, string expectedName)
    {
        _spells.Setup(s => s.SearchAsync(ExpansionId, "frappe", locale, 20, default)).ReturnsAsync(
        [
            new SpellAvailability
            {
                SpellId = 47488, ExpansionId = ExpansionId,
                NameEn = "Frappe mortelle (EN)",
                NameFr = "Frappe mortelle (FR)",
                NameDe = "Frappe mortelle (DE)",
                IconUrl = "https://cdn/mortal-strike.jpg",
            },
        ]);

        var result = await _sut.HandleAsync(MakeQuery(locale), default);

        result.IsSuccess.Should().BeTrue();
        var spell = result.Value.Should().ContainSingle().Subject;
        spell.Id.Should().Be(47488);
        spell.Name.Should().Be(expectedName);
        spell.IconUrl.Should().Be("https://cdn/mortal-strike.jpg");
    }

    [Fact]
    public async Task HandleAsync_SearchUsesTheExpansionFromTheQuery()
    {
        var otherQuery = new SearchSpellsQuery { ExpansionId = 12, SearchTerm = "frappe", Locale = "en" };
        _spells.Setup(s => s.SearchAsync(12, "frappe", "en", 20, default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(otherQuery, default);

        result.IsSuccess.Should().BeTrue();
        _spells.Verify(s => s.SearchAsync(12, "frappe", "en", 20, default), Times.Once);
        _spells.Verify(s => s.SearchAsync(ExpansionId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), default), Times.Never);
    }
}
