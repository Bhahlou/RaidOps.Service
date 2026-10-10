using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RaidOps.API.Controllers.v1;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Spells.Queries;
using RaidOps.Application.Contracts.Raids.Spells.Responses;

namespace RaidOps.UnitTests.Controllers;

/// <summary>Unit tests for <see cref="SpellsController"/>.</summary>
public class SpellsControllerTests
{
    private readonly Mock<ICommandDispatcher> _commands = new();
    private readonly Mock<IQueryDispatcher> _queries = new();
    private readonly SpellsController _sut;

    public SpellsControllerTests()
    {
        _sut = new SpellsController(_commands.Object, _queries.Object);
    }

    [Fact]
    public async Task Search_QuerySucceeds_ReturnsOk()
    {
        var spells = new List<SpellResponse> { new() { Id = 16176, Name = "Ancestral Healing", IconUrl = "https://cdn/x.jpg" } };
        _queries.Setup(q => q.DispatchAsync<SearchSpellsQuery, List<SpellResponse>>(
                It.Is<SearchSpellsQuery>(query => query.ExpansionId == 12 && query.SearchTerm == "heal" && query.Locale == "en"), default))
            .ReturnsAsync(Result<List<SpellResponse>>.Ok(spells));

        var result = await _sut.Search(12, "heal", "en", default);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(spells);
    }

    [Fact]
    public async Task Search_QueryFails_ReturnsBadRequest()
    {
        _queries.Setup(q => q.DispatchAsync<SearchSpellsQuery, List<SpellResponse>>(It.IsAny<SearchSpellsQuery>(), default))
            .ReturnsAsync(Result<List<SpellResponse>>.Fail(ResponseDetail.InvalidRequest));

        var result = await _sut.Search(12, "heal", "en", default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
