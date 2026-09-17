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
    private readonly Mock<IGuildAttributionDefinitionsRepository> _definitions = new();
    private readonly GetGuildAttributionDefinitionsQueryHandler _sut;

    private const string GuildId = "guild-1";
    private const string RequesterId = "officer-1";

    private static readonly GetGuildAttributionDefinitionsQuery Query = new() { GuildId = GuildId, RequesterDiscordId = RequesterId };

    public GetGuildAttributionDefinitionsQueryHandlerTests()
    {
        _sut = new GetGuildAttributionDefinitionsQueryHandler(_access.Object, _definitions.Object);
    }

    [Fact]
    public async Task HandleAsync_NotOfficer_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_Officer_ReturnsMappedDefinitions()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _definitions.Setup(d => d.GetForGuildAsync(GuildId, default)).ReturnsAsync(
        [
            new GuildAttributionDefinition
            {
                Id = 1, GuildId = GuildId, Label = "Innervate", Section = "Personals", IsRepeatable = true, SortOrder = 0,
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
        var query = new GetGuildAttributionDefinitionsQuery { GuildId = GuildId, RequesterDiscordId = RequesterId, RaidBossId = 14 };
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _definitions.Setup(d => d.GetForGuildAsync(GuildId, 14, default)).ReturnsAsync(
        [
            new GuildAttributionDefinition
            {
                Id = 2, GuildId = GuildId, RaidBossId = 14, Label = "Interrupt", SortOrder = 0,
                Cells = [new AttributionDefinitionCell { Id = 20, Kind = AttributionCellKind.NameSlot, SlotLabel = "Interrupt" }],
            },
        ]);

        var result = await _sut.HandleAsync(query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(d => d.Id == 2 && d.RaidBossId == 14);
        _definitions.Verify(d => d.GetForGuildAsync(GuildId, 14, default), Times.Once);
    }
}
