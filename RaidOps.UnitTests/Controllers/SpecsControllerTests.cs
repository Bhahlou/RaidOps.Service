using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RaidOps.API.Controllers.v1;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Specs.Queries;
using RaidOps.Application.Contracts.Specs.Responses;

namespace RaidOps.UnitTests.Controllers;

public class SpecsControllerTests
{
    private readonly Mock<ICommandDispatcher> _commands = new();
    private readonly Mock<IQueryDispatcher>   _queries  = new();
    private readonly SpecsController          _sut;

    public SpecsControllerTests()
    {
        _sut = new SpecsController(_commands.Object, _queries.Object)
        {
            ControllerContext = ControllerTestHelpers.MakeContext()
        };
    }

    [Fact]
    public async Task GetAll_QuerySucceeds_ReturnsOkWithSpecs()
    {
        var specs = new List<SpecDto>
        {
            new() { Id = 71, Name = "Arms", Role = "Dps", ClassId = 1 },
        };
        _queries.Setup(q => q.DispatchAsync<GetSpecsQuery, IEnumerable<SpecDto>>(
                It.IsAny<GetSpecsQuery>(), default))
            .ReturnsAsync(Result<IEnumerable<SpecDto>>.Ok(specs));

        var result = await _sut.GetAll(default);

        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeSameAs(specs);
    }

    [Fact]
    public async Task GetAll_QueryFails_ReturnsBadRequest()
    {
        _queries.Setup(q => q.DispatchAsync<GetSpecsQuery, IEnumerable<SpecDto>>(
                It.IsAny<GetSpecsQuery>(), default))
            .ReturnsAsync(Result<IEnumerable<SpecDto>>.Fail("some-error"));

        var result = await _sut.GetAll(default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
