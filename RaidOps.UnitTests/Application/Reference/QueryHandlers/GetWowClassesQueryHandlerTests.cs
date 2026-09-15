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
    private readonly GetWowClassesQueryHandler _sut;

    private static readonly GetWowClassesQuery Query = new();

    public GetWowClassesQueryHandlerTests()
    {
        _sut = new GetWowClassesQueryHandler(_wowClasses.Object);
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
}
