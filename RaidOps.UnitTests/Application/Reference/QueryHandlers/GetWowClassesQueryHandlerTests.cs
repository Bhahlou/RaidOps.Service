using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Reference.Queries;
using RaidOps.Application.Implementations.Reference.QueryHandlers;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Reference.QueryHandlers;

/// <summary>
/// Unit tests for <see cref="GetWowClassesQueryHandler"/>.
/// </summary>
public class GetWowClassesQueryHandlerTests
{
    private readonly Mock<IWowClassRepository> _wowClasses = new();
    private readonly Mock<IExpansionRepository> _expansions = new();
    private readonly GetWowClassesQueryHandler _sut;

    private static readonly GetWowClassesQuery Query = new();

    public GetWowClassesQueryHandlerTests()
    {
        _sut = new GetWowClassesQueryHandler(_wowClasses.Object, _expansions.Object);
    }

    [Fact]
    public async Task HandleAsync_ReturnsMappedWowClassDtos()
    {
        _wowClasses.Setup(r => r.GetAllAsync(default)).ReturnsAsync(
        [
            new WowClass { Id = 1, Name = "Warrior", Color = "C79C6E", FirstExpansionId = 1 },
            new WowClass { Id = 10, Name = "Monk", Color = "00FF96", FirstExpansionId = 5 },
        ]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);

        var warrior = result.Value.Single(c => c.Id == 1);
        warrior.Name.Should().Be("Warrior");
        warrior.Color.Should().Be("C79C6E");
        warrior.FirstExpansionId.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_EmptyTable_ReturnsOkWithEmptyCollection()
    {
        _wowClasses.Setup(r => r.GetAllAsync(default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_AvailableForExpansionIdSet_FiltersOutClassesNotAvailableOnTargetExpansion()
    {
        _wowClasses.Setup(r => r.GetAllAsync(default)).ReturnsAsync(
        [
            new WowClass { Id = 1, Name = "Warrior", Color = "C79C6E", FirstExpansionId = 1 },
            new WowClass { Id = 6, Name = "Death Knight", Color = "C41F3B", FirstExpansionId = 3 },
        ]);
        _expansions.Setup(r => r.GetAllAsync(default)).ReturnsAsync(
        [
            new Expansion { Id = 1, Name = "Classic", ShortCode = "Classic", ReleaseOrder = 1 },
            new Expansion { Id = 3, Name = "WotLK", ShortCode = "WotLK", ReleaseOrder = 3 },
            new Expansion { Id = 12, Name = "Forever", ShortCode = "Forever", ReleaseOrder = 12, ForkedFromExpansionId = 1 },
        ]);

        var query = new GetWowClassesQuery { AvailableForExpansionId = 12 };
        var result = await _sut.HandleAsync(query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Select(c => c.Id).Should().BeEquivalentTo([1]);
    }

    [Fact]
    public async Task HandleAsync_AvailableForExpansionIdNotFound_ReturnsAllClassesUnfiltered()
    {
        _wowClasses.Setup(r => r.GetAllAsync(default)).ReturnsAsync(
        [
            new WowClass { Id = 1, Name = "Warrior", Color = "C79C6E", FirstExpansionId = 1 },
            new WowClass { Id = 6, Name = "Death Knight", Color = "C41F3B", FirstExpansionId = 3 },
        ]);
        _expansions.Setup(r => r.GetAllAsync(default)).ReturnsAsync(
        [
            new Expansion { Id = 1, Name = "Classic", ShortCode = "Classic", ReleaseOrder = 1 },
        ]);

        var query = new GetWowClassesQuery { AvailableForExpansionId = 999 };
        var result = await _sut.HandleAsync(query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Select(c => c.Id).Should().BeEquivalentTo([1, 6]);
    }

    [Fact]
    public async Task HandleAsync_ClassFirstExpansionIdHasNoMatchingExpansionRow_ExcludesThatClass()
    {
        _wowClasses.Setup(r => r.GetAllAsync(default)).ReturnsAsync(
        [
            new WowClass { Id = 1, Name = "Warrior", Color = "C79C6E", FirstExpansionId = 1 },
            new WowClass { Id = 99, Name = "Orphaned", Color = "000000", FirstExpansionId = 404 },
        ]);
        _expansions.Setup(r => r.GetAllAsync(default)).ReturnsAsync(
        [
            new Expansion { Id = 1, Name = "Classic", ShortCode = "Classic", ReleaseOrder = 1 },
        ]);

        var query = new GetWowClassesQuery { AvailableForExpansionId = 1 };
        var result = await _sut.HandleAsync(query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Select(c => c.Id).Should().BeEquivalentTo([1]);
    }
}
