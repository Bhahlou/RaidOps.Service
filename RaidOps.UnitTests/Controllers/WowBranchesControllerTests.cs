using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RaidOps.API.Controllers.v1;
using RaidOps.Application.Contracts.Branches.Queries;
using RaidOps.Application.Contracts.Branches.Responses;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.UnitTests.Controllers;

public class WowBranchesControllerTests
{
    private readonly Mock<ICommandDispatcher> _commands = new();
    private readonly Mock<IQueryDispatcher>   _queries  = new();
    private readonly WowBranchesController    _sut;

    public WowBranchesControllerTests()
    {
        _sut = new WowBranchesController(_commands.Object, _queries.Object)
        {
            ControllerContext = ControllerTestHelpers.MakeContext()
        };
    }

    [Fact]
    public async Task GetAll_QuerySucceeds_ReturnsOkWithBranches()
    {
        var branches = new List<BranchDto>
        {
            new() { Id = 1, Name = "Retail", BnetNamespacePrefix = "dynamic", CurrentExpansionShortCode = "TWW" },
        };
        _queries.Setup(q => q.DispatchAsync<GetBranchesQuery, IEnumerable<BranchDto>>(
                It.IsAny<GetBranchesQuery>(), default))
            .ReturnsAsync(Result<IEnumerable<BranchDto>>.Ok(branches));

        var result = await _sut.GetAll(default);

        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeSameAs(branches);
    }

    [Fact]
    public async Task GetAll_QueryFails_ReturnsBadRequest()
    {
        _queries.Setup(q => q.DispatchAsync<GetBranchesQuery, IEnumerable<BranchDto>>(
                It.IsAny<GetBranchesQuery>(), default))
            .ReturnsAsync(Result<IEnumerable<BranchDto>>.Fail("some-error"));

        var result = await _sut.GetAll(default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
