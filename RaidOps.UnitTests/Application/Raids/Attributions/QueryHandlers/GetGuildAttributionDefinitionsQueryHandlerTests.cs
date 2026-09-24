using RaidOps.Domain.Models.Reference;
using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Attributions.Queries;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Attributions.QueryHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids.Attributions;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Attributions.QueryHandlers;

/// <summary>
/// Unit tests for <see cref="GetGuildAttributionDefinitionsQueryHandler"/>.
/// </summary>
public class GetGuildAttributionDefinitionsQueryHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IGuildBranchesRepository> _branches = new();
    private readonly Mock<IGuildAttributionDefinitionsRepository> _definitions = new();
    private readonly GetGuildAttributionDefinitionsQueryHandler _sut;

    private const string GuildId = "guild-1";
    private const string RequesterId = "officer-1";
    private const int BranchId = 5;

    private static readonly GetGuildAttributionDefinitionsQuery Query = new() { GuildId = GuildId, GuildBranchId = BranchId, RequesterDiscordId = RequesterId };

    public GetGuildAttributionDefinitionsQueryHandlerTests()
    {
        _branches.Setup(b => b.GetCurrentExpansionIdAsync(GuildId, BranchId, default)).ReturnsAsync(2);
        _sut = new GetGuildAttributionDefinitionsQueryHandler(_access.Object, _branches.Object, _definitions.Object);
    }

    [Fact]
    public async Task HandleAsync_NotOfficer_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, BranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_Officer_ReturnsMappedDefinitions()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, BranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _definitions.Setup(d => d.GetForBranchAsync(GuildId, BranchId, null, default)).ReturnsAsync(
        [
            new GuildAttributionDefinition
            {
                Id = 1, GuildId = GuildId, GuildBranchId = BranchId, Label = "Innervate", Section = "Personals", IsRepeatable = true, SortOrder = 0,
                Cells = [new AttributionDefinitionCell { Id = 10, Kind = AttributionCellKind.NameSlot, SlotLabel = "De" }],
            },
        ]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        var definition = result.Value.Single();
        definition.Id.Should().Be(1);
        definition.Label.Should().Be("Innervate");
        definition.Section.Should().Be("Personals");
        definition.IsRepeatable.Should().BeTrue();
        definition.Cells.Should().ContainSingle(c => c.Id == 10 && c.SlotLabel == "De");
    }

    [Fact]
    public async Task HandleAsync_RaidBossIdGiven_PassesItThroughToTheRepository()
    {
        var query = new GetGuildAttributionDefinitionsQuery { GuildId = GuildId, GuildBranchId = BranchId, RequesterDiscordId = RequesterId, RaidBossId = 14 };
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, BranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _definitions.Setup(d => d.GetForBranchAsync(GuildId, BranchId, 14, default)).ReturnsAsync(
        [
            new GuildAttributionDefinition
            {
                Id = 2, GuildId = GuildId, GuildBranchId = BranchId, RaidBossId = 14, Label = "Interrupt", SortOrder = 0,
                Cells = [new AttributionDefinitionCell { Id = 20, Kind = AttributionCellKind.NameSlot, SlotLabel = "Interrupt" }],
            },
        ]);

        var result = await _sut.HandleAsync(query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(d => d.Id == 2 && d.RaidBossId == 14);
        _definitions.Verify(d => d.GetForBranchAsync(GuildId, BranchId, 14, default), Times.Once);
    }

    // ── Guild-branch scoping ─────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_OfficerOfAnotherBranchOnly_ReturnsForbiddenUsingTheBranchAwareOverload()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, 99, default)).ReturnsAsync(GuildAccessLevel.Officer);

        var result = await _sut.HandleAsync(Query, default);

        result.Error.Should().Be(ResponseDetail.Forbidden);
        _access.Verify(a => a.GetAccessLevelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_GuildBranchNotFound_ReturnsGuildBranchNotFound()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, BranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _branches.Setup(b => b.GetCurrentExpansionIdAsync(GuildId, BranchId, default)).ReturnsAsync((int?)null);

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildBranchNotFound);
        _definitions.Verify(d => d.GetForBranchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int?>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SpellIconsAreResolvedOnTheBranchsOwnExpansion()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, BranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _definitions.Setup(d => d.GetForBranchAsync(GuildId, BranchId, null, default)).ReturnsAsync(
        [
            new GuildAttributionDefinition
            {
                Id = 1, GuildId = GuildId, GuildBranchId = BranchId, Label = "Bloodlust",
                Cells =
                [
                    new AttributionDefinitionCell
                    {
                        Id = 10, Kind = AttributionCellKind.Icon, IconSource = AttributionIconSource.Spell, SpellId = 2825,
                        Spell = new Spell
                        {
                            Id = 2825,
                            Availabilities =
                            [
                                new SpellAvailability { SpellId = 2825, ExpansionId = 12, IconUrl = "https://cdn/forever.jpg" },
                                new SpellAvailability { SpellId = 2825, ExpansionId = 2, IconUrl = "https://cdn/tbc.jpg" },
                            ],
                        },
                    },
                ],
            },
        ]);

        var result = await _sut.HandleAsync(Query, default);

        result.Value!.Single().Cells.Single().SpellIconUrl.Should().Be("https://cdn/tbc.jpg");
    }
}
