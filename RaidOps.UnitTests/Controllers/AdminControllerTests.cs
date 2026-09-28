using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RaidOps.API.Controllers.v1;
using RaidOps.API.Requests;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Buffs;
using RaidOps.Application.Contracts.Raids.Buffs.Commands;
using RaidOps.Application.Contracts.Raids.Spells.Commands;
using RaidOps.Domain.Enums;

namespace RaidOps.UnitTests.Controllers;

/// <summary>
/// Unit tests for <see cref="AdminController"/>'s action bodies. The owner-only gate itself lives in
/// <see cref="RaidOps.API.Authorization.OwnerOnlyAttribute"/> (a filter, so it never runs when calling
/// an action method directly) — see <see cref="OwnerOnlyAttributeTests"/> for that.
/// </summary>
public class AdminControllerTests
{
    private readonly Mock<ICommandDispatcher> _commands = new();
    private readonly Mock<IQueryDispatcher> _queries = new();

    private AdminController MakeSut() => new(_commands.Object, _queries.Object);

    private static RaidBuffDefinitionInput MakeDefinition() => new()
    {
        SpellId = 16176,
        Scope = RaidBuffScope.Raid,
        Kind = RaidBuffKind.Buff,
        LabelEn = "+25% armor",
        LabelFr = "+25 % d'armure",
        LabelDe = "+25 % Rüstung",
        SortOrder = 10,
        Sources = [new RaidBuffSourceDto { ClassId = 7, SpecId = 264 }],
    };

    // ── SyncSpells ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SyncSpells_DispatchesForcedSyncAndReturnsTheResult()
    {
        var body = new List<string> { "branch result" };
        _commands.Setup(c => c.DispatchAsync(It.IsAny<SyncSpellsCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("1 branch(es) checked.", body)));

        var result = await MakeSut().SyncSpells(default);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<CommandResponse>().Which.Body.Should().BeSameAs(body);
        _commands.Verify(c => c.DispatchAsync(It.Is<SyncSpellsCommand>(x => x.Force), default), Times.Once);
    }

    [Fact]
    public async Task SyncSpells_CommandFails_ReturnsBadRequest()
    {
        _commands.Setup(c => c.DispatchAsync(It.IsAny<SyncSpellsCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, "nope"));

        var result = await MakeSut().SyncSpells(default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── SaveRaidBuff ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveRaidBuff_DispatchesAnUpsertOfASingleDefinitionForTheExpansion()
    {
        var definition = MakeDefinition();
        _commands.Setup(c => c.DispatchAsync(It.IsAny<UpsertRaidBuffDefinitionsCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));

        var result = await MakeSut().SaveRaidBuff(12, definition, default);

        result.Should().BeOfType<OkObjectResult>();
        _commands.Verify(c => c.DispatchAsync(
            It.Is<UpsertRaidBuffDefinitionsCommand>(cmd => cmd.ExpansionId == 12 && cmd.Definitions.Single() == definition && !cmd.PruneMissing),
            default), Times.Once);
    }

    [Fact]
    public async Task SaveRaidBuff_CommandFails_ReturnsBadRequest()
    {
        _commands.Setup(c => c.DispatchAsync(It.IsAny<UpsertRaidBuffDefinitionsCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, "nope"));

        var result = await MakeSut().SaveRaidBuff(12, MakeDefinition(), default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── ImportRaidBuffs ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ImportRaidBuffs_DispatchesAnUpsertOfTheWholeListWithThePruneFlag(bool pruneMissing)
    {
        var definitions = new List<RaidBuffDefinitionInput> { MakeDefinition() };
        _commands.Setup(c => c.DispatchAsync(It.IsAny<UpsertRaidBuffDefinitionsCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));

        var result = await MakeSut().ImportRaidBuffs(12, new ImportRaidBuffDefinitionsRequest { Definitions = definitions, PruneMissing = pruneMissing }, default);

        result.Should().BeOfType<OkObjectResult>();
        _commands.Verify(c => c.DispatchAsync(
            It.Is<UpsertRaidBuffDefinitionsCommand>(cmd => cmd.ExpansionId == 12 && cmd.Definitions.SequenceEqual(definitions) && cmd.PruneMissing == pruneMissing),
            default), Times.Once);
    }

    [Fact]
    public async Task ImportRaidBuffs_CommandFails_ReturnsBadRequest()
    {
        _commands.Setup(c => c.DispatchAsync(It.IsAny<UpsertRaidBuffDefinitionsCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, "nope"));

        var result = await MakeSut().ImportRaidBuffs(12, new ImportRaidBuffDefinitionsRequest { Definitions = [MakeDefinition()] }, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── UpdateRaidBuff ───────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateRaidBuff_DispatchesTheEditByIdCommand()
    {
        var definition = MakeDefinition();
        _commands.Setup(c => c.DispatchAsync(It.IsAny<UpdateRaidBuffDefinitionCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));

        var result = await MakeSut().UpdateRaidBuff(42, definition, default);

        result.Should().BeOfType<OkObjectResult>();
        _commands.Verify(c => c.DispatchAsync(It.Is<UpdateRaidBuffDefinitionCommand>(cmd => cmd.Id == 42 && cmd.Definition == definition), default), Times.Once);
    }

    [Fact]
    public async Task UpdateRaidBuff_CommandFails_ReturnsBadRequest()
    {
        _commands.Setup(c => c.DispatchAsync(It.IsAny<UpdateRaidBuffDefinitionCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.NotFound, "nope"));

        var result = await MakeSut().UpdateRaidBuff(42, MakeDefinition(), default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── DeleteRaidBuff ───────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteRaidBuff_DispatchesTheDeleteCommand()
    {
        _commands.Setup(c => c.DispatchAsync(It.IsAny<DeleteRaidBuffDefinitionCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));

        var result = await MakeSut().DeleteRaidBuff(7, default);

        result.Should().BeOfType<OkObjectResult>();
        _commands.Verify(c => c.DispatchAsync(It.Is<DeleteRaidBuffDefinitionCommand>(cmd => cmd.Id == 7), default), Times.Once);
    }

    [Fact]
    public async Task DeleteRaidBuff_CommandFails_ReturnsBadRequest()
    {
        _commands.Setup(c => c.DispatchAsync(It.IsAny<DeleteRaidBuffDefinitionCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.NotFound, "nope"));

        var result = await MakeSut().DeleteRaidBuff(7, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
