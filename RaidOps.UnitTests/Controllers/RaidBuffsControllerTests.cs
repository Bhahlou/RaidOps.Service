using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RaidOps.API.Controllers.v1;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Buffs.Queries;
using RaidOps.Application.Contracts.Raids.Buffs.Responses;

namespace RaidOps.UnitTests.Controllers;

/// <summary>Unit tests for <see cref="RaidBuffsController"/>.</summary>
public class RaidBuffsControllerTests
{
    private readonly Mock<ICommandDispatcher> _commands = new();
    private readonly Mock<IQueryDispatcher> _queries = new();
    private readonly RaidBuffsController _sut;

    public RaidBuffsControllerTests()
    {
        _sut = new RaidBuffsController(_commands.Object, _queries.Object);
    }

    [Fact]
    public async Task GetAll_QuerySucceeds_ReturnsOk()
    {
        var definitions = new List<RaidBuffDefinitionResponse>();
        _queries.Setup(q => q.DispatchAsync<GetRaidBuffDefinitionsQuery, List<RaidBuffDefinitionResponse>>(
                It.Is<GetRaidBuffDefinitionsQuery>(query => query.ExpansionId == 12), default))
            .ReturnsAsync(Result<List<RaidBuffDefinitionResponse>>.Ok(definitions));

        var result = await _sut.GetAll(12, default);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(definitions);
    }

    [Fact]
    public async Task GetAll_QueryFails_ReturnsBadRequest()
    {
        _queries.Setup(q => q.DispatchAsync<GetRaidBuffDefinitionsQuery, List<RaidBuffDefinitionResponse>>(It.IsAny<GetRaidBuffDefinitionsQuery>(), default))
            .ReturnsAsync(Result<List<RaidBuffDefinitionResponse>>.Fail(ResponseDetail.InvalidRequest));

        var result = await _sut.GetAll(12, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
