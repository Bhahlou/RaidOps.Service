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
    private readonly Mock<IGuildBranchesRepository> _branches = new();
    private readonly Mock<ISpellRepository> _spells = new();
    private readonly SearchSpellsQueryHandler _sut;

    private const string GuildId = "guild-1";
    private const string RequesterId = "officer-1";
    private const int BranchId = 5;

    private static SearchSpellsQuery MakeQuery(string locale = "en") => new()
    {
        GuildId = GuildId, RequesterDiscordId = RequesterId, GuildBranchId = BranchId, SearchTerm = "frappe", Locale = locale,
    };

    public SearchSpellsQueryHandlerTests()
    {
        _branches.Setup(b => b.GetCurrentExpansionIdAsync(GuildId, BranchId, default)).ReturnsAsync(2);
        _sut = new SearchSpellsQueryHandler(_access.Object, _branches.Object, _spells.Object);
    }

    [Fact]
    public async Task HandleAsync_NotOfficer_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, BranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_SearchesWithinExpansionAndCapsAt20Results()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, BranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
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
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, BranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _spells.Setup(s => s.SearchAsync(2, "frappe", locale, 20, default)).ReturnsAsync(
        [
            new SpellAvailability
            {
                SpellId = 47488, ExpansionId = 2,
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

    // ── Guild-branch scoping ─────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_OfficerOfAnotherBranchOnly_ReturnsForbiddenUsingTheBranchAwareOverload()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, 99, default)).ReturnsAsync(GuildAccessLevel.Officer);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.Error.Should().Be(ResponseDetail.Forbidden);
        _access.Verify(a => a.GetAccessLevelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _spells.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_GuildBranchNotFound_ReturnsGuildBranchNotFound()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, BranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _branches.Setup(b => b.GetCurrentExpansionIdAsync(GuildId, BranchId, default)).ReturnsAsync((int?)null);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildBranchNotFound);
        _spells.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_SearchUsesTheExpansionOfTheRequestedBranch()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, BranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _branches.Setup(b => b.GetCurrentExpansionIdAsync(GuildId, BranchId, default)).ReturnsAsync(12);
        _spells.Setup(s => s.SearchAsync(12, "frappe", "en", 20, default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsSuccess.Should().BeTrue();
        _spells.Verify(s => s.SearchAsync(12, "frappe", "en", 20, default), Times.Once);
        _spells.Verify(s => s.SearchAsync(2, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), default), Times.Never);
    }
}
