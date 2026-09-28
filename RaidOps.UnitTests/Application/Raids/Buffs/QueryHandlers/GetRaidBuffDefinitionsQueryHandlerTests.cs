using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Raids.Buffs.Queries;
using RaidOps.Application.Implementations.Raids.Buffs.QueryHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Buffs.QueryHandlers;

/// <summary>Unit tests for <see cref="GetRaidBuffDefinitionsQueryHandler"/>.</summary>
public class GetRaidBuffDefinitionsQueryHandlerTests
{
    private readonly Mock<IRaidBuffDefinitionsRepository> _definitions = new();
    private readonly GetRaidBuffDefinitionsQueryHandler _sut;

    public GetRaidBuffDefinitionsQueryHandlerTests()
    {
        _sut = new GetRaidBuffDefinitionsQueryHandler(_definitions.Object);
    }

    [Fact]
    public async Task HandleAsync_NoDefinitions_ReturnsAnEmptyList()
    {
        _definitions.Setup(d => d.GetForExpansionAsync(12, default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(new GetRaidBuffDefinitionsQuery { ExpansionId = 12 }, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_MapsEveryDefinitionOfTheRequestedExpansion()
    {
        var definition = new RaidBuffDefinition
        {
            Id = 1, ExpansionId = 12, SpellId = 16176,
            LabelEn = "en", LabelFr = "fr", LabelDe = "de",
            Spell = new Spell { Id = 16176, Availabilities = [] },
        };
        _definitions.Setup(d => d.GetForExpansionAsync(12, default)).ReturnsAsync([definition]);

        var result = await _sut.HandleAsync(new GetRaidBuffDefinitionsQuery { ExpansionId = 12 }, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(r => r.Id == 1 && r.SpellId == 16176);
    }

    [Fact]
    public async Task HandleAsync_UsesTheExpansionFromTheQueryNotAnyOther()
    {
        _definitions.Setup(d => d.GetForExpansionAsync(2, default)).ReturnsAsync([]);

        await _sut.HandleAsync(new GetRaidBuffDefinitionsQuery { ExpansionId = 2 }, default);

        _definitions.Verify(d => d.GetForExpansionAsync(2, default), Times.Once);
        _definitions.Verify(d => d.GetForExpansionAsync(It.Is<int>(id => id != 2), default), Times.Never);
    }
}
