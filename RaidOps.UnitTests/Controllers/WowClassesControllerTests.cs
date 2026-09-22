using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RaidOps.API.Controllers.v1;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Reference.Queries;
using RaidOps.Application.Contracts.Reference.Responses;

namespace RaidOps.UnitTests.Controllers;

public class WowClassesControllerTests
{
    private readonly Mock<ICommandDispatcher> _commands = new();
    private readonly Mock<IQueryDispatcher> _queries = new();
    private readonly WowClassesController _sut;

    public WowClassesControllerTests()
    {
        _sut = new WowClassesController(_commands.Object, _queries.Object)
        {
            ControllerContext = ControllerTestHelpers.MakeContext(),
        };
    }

    [Fact]
    public async Task GetAll_QuerySucceeds_ReturnsOkWithClasses()
    {
        var classes = new List<WowClassDto> { new() { Id = 1, Name = "Warrior", Color = "C79C6E", FirstExpansionId = 1 } };
        _queries.Setup(q => q.DispatchAsync<GetWowClassesQuery, IEnumerable<WowClassDto>>(It.IsAny<GetWowClassesQuery>(), default))
            .ReturnsAsync(Result<IEnumerable<WowClassDto>>.Ok(classes));

        var result = await _sut.GetAll(null, default);

        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeSameAs(classes);
    }

    [Fact]
    public async Task GetAll_AvailableForExpansionIdGiven_ForwardsItOnTheQuery()
    {
        _queries.Setup(q => q.DispatchAsync<GetWowClassesQuery, IEnumerable<WowClassDto>>(
                It.Is<GetWowClassesQuery>(query => query.AvailableForExpansionId == 12), default))
            .ReturnsAsync(Result<IEnumerable<WowClassDto>>.Ok([]));

        var result = await _sut.GetAll(12, default);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetAll_QueryFails_ReturnsBadRequest()
    {
        _queries.Setup(q => q.DispatchAsync<GetWowClassesQuery, IEnumerable<WowClassDto>>(It.IsAny<GetWowClassesQuery>(), default))
            .ReturnsAsync(Result<IEnumerable<WowClassDto>>.Fail("some-error"));

        var result = await _sut.GetAll(null, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
