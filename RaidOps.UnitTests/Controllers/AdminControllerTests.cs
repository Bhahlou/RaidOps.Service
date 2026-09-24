using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RaidOps.API.Controllers.v1;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Spells.Commands;

namespace RaidOps.UnitTests.Controllers;

/// <summary>Unit tests for <see cref="AdminController"/>.</summary>
public class AdminControllerTests
{
    private readonly Mock<ICommandDispatcher> _commands = new();
    private readonly Mock<IQueryDispatcher> _queries = new();

    private const string OwnerId = "111111111111111111";

    private AdminController MakeSut(string? ownerIds, string? discordId = OwnerId)
    {
        var config = ControllerTestHelpers.MakeConfig();
        config.Setup(c => c["Admin:OwnerDiscordIds"]).Returns(ownerIds);

        return new AdminController(_commands.Object, _queries.Object, config.Object)
        {
            ControllerContext = discordId is null
                ? ControllerTestHelpers.MakeAnonymousContext()
                : ControllerTestHelpers.MakeContext(discordId),
        };
    }

    [Fact]
    public async Task SyncSpells_SubClaimMissing_ReturnsUnauthorized()
    {
        var sut = MakeSut(OwnerId, discordId: null);

        var result = await sut.SyncSpells(default);

        result.Should().BeOfType<UnauthorizedResult>();
        _commands.Verify(c => c.DispatchAsync(It.IsAny<SyncSpellsCommand>(), default), Times.Never);
    }

    [Fact]
    public async Task SyncSpells_SubNotInOwnerList_ReturnsForbid()
    {
        var sut = MakeSut("222222222222222222,333333333333333333");

        var result = await sut.SyncSpells(default);

        result.Should().BeOfType<ForbidResult>();
        _commands.Verify(c => c.DispatchAsync(It.IsAny<SyncSpellsCommand>(), default), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" , ,")]
    public async Task SyncSpells_OwnerListMissingOrEmpty_ReturnsForbid(string? ownerIds)
    {
        var sut = MakeSut(ownerIds);

        var result = await sut.SyncSpells(default);

        result.Should().BeOfType<ForbidResult>();
        _commands.Verify(c => c.DispatchAsync(It.IsAny<SyncSpellsCommand>(), default), Times.Never);
    }

    [Fact]
    public async Task SyncSpells_OwnerListIsAPrefixOfTheSub_ReturnsForbid()
    {
        // Entries must match exactly — a shorter/longer ID that merely overlaps is not the owner.
        var sut = MakeSut("11111111111111111");

        var result = await sut.SyncSpells(default);

        result.Should().BeOfType<ForbidResult>();
    }

    [Theory]
    [InlineData(OwnerId)]
    [InlineData("222222222222222222, 111111111111111111 ,333333333333333333")]
    [InlineData("  111111111111111111  ")]
    [InlineData("222222222222222222,,111111111111111111")]
    public async Task SyncSpells_SubInCommaSeparatedList_DispatchesForcedSyncAndReturnsTheResult(string ownerIds)
    {
        var body = new List<string> { "branch result" };
        _commands.Setup(c => c.DispatchAsync(It.IsAny<SyncSpellsCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("1 branch(es) checked.", body)));
        var sut = MakeSut(ownerIds);

        var result = await sut.SyncSpells(default);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<CommandResponse>().Which.Body.Should().BeSameAs(body);
        _commands.Verify(c => c.DispatchAsync(It.Is<SyncSpellsCommand>(x => x.Force), default), Times.Once);
    }

    [Fact]
    public async Task SyncSpells_CommandFails_ReturnsBadRequest()
    {
        _commands.Setup(c => c.DispatchAsync(It.IsAny<SyncSpellsCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, "nope"));
        var sut = MakeSut(OwnerId);

        var result = await sut.SyncSpells(default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
