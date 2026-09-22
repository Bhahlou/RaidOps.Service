using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Spells.Queries;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Spells.QueryHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Spells.QueryHandlers;

/// <summary>
/// Unit tests for <see cref="SearchSpellsQueryHandler"/>.
/// </summary>
public class SearchSpellsQueryHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<ISpellRepository> _spells = new();
    private readonly SearchSpellsQueryHandler _sut;

    private const string GuildId = "guild-1";
    private const string RequesterId = "officer-1";

    private static SearchSpellsQuery MakeQuery(string locale = "en") => new()
    {
        GuildId = GuildId, RequesterDiscordId = RequesterId, ExpansionId = 2, SearchTerm = "frappe", Locale = locale,
    };

    public SearchSpellsQueryHandlerTests()
    {
        _sut = new SearchSpellsQueryHandler(_access.Object, _spells.Object);
    }

    [Fact]
    public async Task HandleAsync_NotOfficer_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_SearchesWithinExpansionAndCapsAt20Results()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _spells.Setup(s => s.SearchAsync(2, "frappe", "en", 20, default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsSuccess.Should().BeTrue();
        _spells.Verify(s => s.SearchAsync(2, "frappe", "en", 20, default), Times.Once);
    }

    [Theory]
    [InlineData("fr", "Frappe mortelle (FR)")]
    [InlineData("de", "Frappe mortelle (DE)")]
    [InlineData("en", "Frappe mortelle (EN)")]
    [InlineData("es", "Frappe mortelle (EN)")]
    public async Task HandleAsync_ReturnsNameLocalizedForTheRequesterLocale(string locale, string expectedName)
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _spells.Setup(s => s.SearchAsync(2, "frappe", locale, 20, default)).ReturnsAsync(
        [
            new Spell
            {
                Id = 47488,
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
}
