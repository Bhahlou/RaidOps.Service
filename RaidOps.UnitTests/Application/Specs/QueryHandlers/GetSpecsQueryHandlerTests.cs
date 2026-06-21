using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Specs.Queries;
using RaidOps.Application.Implementations.Specs.QueryHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Specs.QueryHandlers;

public class GetSpecsQueryHandlerTests
{
    private readonly Mock<ISpecRepository> _specs = new();
    private readonly GetSpecsQueryHandler  _sut;

    private static readonly GetSpecsQuery Query = new();

    public GetSpecsQueryHandlerTests()
    {
        _sut = new GetSpecsQueryHandler(_specs.Object);
    }

    [Fact]
    public async Task HandleAsync_ReturnsMappedSpecDtos()
    {
        _specs.Setup(r => r.GetAllAsync(default))
            .ReturnsAsync(
            [
                new Spec { Id = 71, Name = "Arms", Role = SpecRole.Dps, ClassId = 1, IconUrl = "https://cdn/arms.jpg" },
                new Spec { Id = 73, Name = "Protection", Role = SpecRole.Tank, ClassId = 1 },
            ]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);

        var arms = result.Value.Single(s => s.Id == 71);
        arms.Name.Should().Be("Arms");
        arms.Role.Should().Be("Dps");
        arms.ClassId.Should().Be(1);
        arms.IconUrl.Should().Be("https://cdn/arms.jpg");

        var prot = result.Value.Single(s => s.Id == 73);
        prot.Role.Should().Be("Tank");
        prot.IconUrl.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_EmptyTable_ReturnsOkWithEmptyCollection()
    {
        _specs.Setup(r => r.GetAllAsync(default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
