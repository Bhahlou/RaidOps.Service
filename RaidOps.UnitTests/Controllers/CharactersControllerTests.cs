using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RaidOps.API.Controllers.v1;
using RaidOps.API.Requests;
using RaidOps.Application.Contracts.Characters.Commands;
using RaidOps.Application.Contracts.Characters.Queries;
using RaidOps.Application.Contracts.Characters.Responses;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.UnitTests.Controllers;

public class CharactersControllerTests
{
    private readonly Mock<ICommandDispatcher> _commands = new();
    private readonly Mock<IQueryDispatcher>   _queries  = new();
    private readonly CharactersController     _sut;

    private const string DiscordId = "user-1";

    public CharactersControllerTests()
    {
        _sut = new CharactersController(_commands.Object, _queries.Object)
        {
            ControllerContext = ControllerTestHelpers.MakeContext(DiscordId)
        };
    }

    // ── GetAll ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_SubMissing_ReturnsUnauthorized()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();
        (await _sut.GetAll(default)).Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetAll_QuerySucceeds_ReturnsOk()
    {
        _queries.Setup(q => q.DispatchAsync<GetCharactersQuery, GetCharactersResponse>(
                It.Is<GetCharactersQuery>(x => x.UserDiscordId == DiscordId), default))
            .ReturnsAsync(Result<GetCharactersResponse>.Ok(new GetCharactersResponse()));

        (await _sut.GetAll(default)).Should().BeOfType<OkObjectResult>();
    }

    // ── GetSynced ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSynced_SubMissing_ReturnsUnauthorized()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();
        (await _sut.GetSynced(default)).Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetSynced_QuerySucceeds_ReturnsOk()
    {
        _queries.Setup(q => q.DispatchAsync<GetSyncedCharactersQuery, IEnumerable<SyncedCharacterDto>>(
                It.IsAny<GetSyncedCharactersQuery>(), default))
            .ReturnsAsync(Result<IEnumerable<SyncedCharacterDto>>.Ok([]));

        (await _sut.GetSynced(default)).Should().BeOfType<OkObjectResult>();
    }

    // ── Sync ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Sync_SubMissing_ReturnsUnauthorized()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();
        (await _sut.Sync(new SyncBnetCharactersRequest { BranchId = 1 }, default))
            .Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Sync_CommandSucceeds_ReturnsOk()
    {
        _commands.Setup(c => c.DispatchAsync(It.IsAny<SyncBnetCharactersCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));

        (await _sut.Sync(new SyncBnetCharactersRequest { BranchId = 1 }, default))
            .Should().BeOfType<OkObjectResult>();
    }

    // ── Activate ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Activate_SubMissing_ReturnsUnauthorized()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();
        (await _sut.Activate(new ActivateCharactersRequest { CharacterIds = [] }, default))
            .Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Activate_CommandSucceeds_ReturnsOk()
    {
        _commands.Setup(c => c.DispatchAsync(It.IsAny<ActivateCharactersCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));

        (await _sut.Activate(new ActivateCharactersRequest { CharacterIds = [1, 2] }, default))
            .Should().BeOfType<OkObjectResult>();
    }

    // ── GetCharacter ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCharacter_SubMissing_ReturnsUnauthorized()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();
        (await _sut.GetCharacter("classic-anniversary", "kazzak", "arthas", default))
            .Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetCharacter_QueryFails_ReturnsBadRequest()
    {
        _queries.Setup(q => q.DispatchAsync<GetCharacterQuery, CharacterDetailResponse>(
                It.IsAny<GetCharacterQuery>(), default))
            .ReturnsAsync(Result<CharacterDetailResponse>.Fail(ResponseDetail.NotFound));

        (await _sut.GetCharacter("classic-anniversary", "kazzak", "arthas", default))
            .Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetCharacter_Success_ReturnsOkWithResponse()
    {
        var response = new CharacterDetailResponse
        {
            Character = new CharacterDto { Id = 10, Name = "Arthas" },
            IsOwner = false,
            CanEditRaidSpecs = true,
        };
        _queries.Setup(q => q.DispatchAsync<GetCharacterQuery, CharacterDetailResponse>(
                It.IsAny<GetCharacterQuery>(), default))
            .ReturnsAsync(Result<CharacterDetailResponse>.Ok(response));

        var result = await _sut.GetCharacter("classic-anniversary", "kazzak", "arthas", default);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(response);
    }

    [Fact]
    public async Task GetCharacter_PassesCorrectQueryFields()
    {
        _queries.Setup(q => q.DispatchAsync<GetCharacterQuery, CharacterDetailResponse>(
                It.IsAny<GetCharacterQuery>(), default))
            .ReturnsAsync(Result<CharacterDetailResponse>.Ok(new CharacterDetailResponse
            {
                Character = new CharacterDto { Id = 10, Name = "Arthas" },
                IsOwner = true,
                CanEditRaidSpecs = true,
            }));

        await _sut.GetCharacter("classic-anniversary", "kazzak", "arthas", default);

        _queries.Verify(q => q.DispatchAsync<GetCharacterQuery, CharacterDetailResponse>(
            It.Is<GetCharacterQuery>(x =>
                x.BranchSlug == "classic-anniversary" && x.RealmSlug == "kazzak" &&
                x.CharacterName == "arthas" && x.RequesterDiscordId == DiscordId),
            default), Times.Once);
    }

    // ── Resync ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Resync_SubMissing_ReturnsUnauthorized()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();
        (await _sut.Resync(10, default)).Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Resync_CommandSucceeds_ReturnsOkWithBody()
    {
        var dto = new CharacterDto { Id = 10, Name = "Arthas" };
        _commands.Setup(c => c.DispatchAsync(It.IsAny<ResyncCharacterCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("resynced", dto)));

        var result = await _sut.Resync(10, default);

        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().Be(dto);
    }

    [Fact]
    public async Task Resync_CommandFails_ReturnsBadRequest()
    {
        _commands.Setup(c => c.DispatchAsync(It.IsAny<ResyncCharacterCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.NotFound));

        (await _sut.Resync(10, default)).Should().BeOfType<BadRequestObjectResult>();
    }

    // ── SetRaidSpecs ──────────────────────────────────────────────────────────

    [Fact]
    public async Task SetRaidSpecs_SubMissing_ReturnsUnauthorized()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();
        (await _sut.SetRaidSpecs(10, new SetCharacterRaidSpecsRequest { MainSpecId = 71, ViableSpecIds = [71] }, default))
            .Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task SetRaidSpecs_CommandSucceeds_ReturnsOk()
    {
        _commands.Setup(c => c.DispatchAsync(It.IsAny<SetCharacterRaidSpecsCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));

        (await _sut.SetRaidSpecs(10, new SetCharacterRaidSpecsRequest { MainSpecId = 71, ViableSpecIds = [71, 72] }, default))
            .Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SetRaidSpecs_CommandFails_ReturnsBadRequest()
    {
        _commands.Setup(c => c.DispatchAsync(It.IsAny<SetCharacterRaidSpecsCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest));

        (await _sut.SetRaidSpecs(10, new SetCharacterRaidSpecsRequest { MainSpecId = 71, ViableSpecIds = [71] }, default))
            .Should().BeOfType<BadRequestObjectResult>();
    }

    // ── Deactivate ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Deactivate_SubMissing_ReturnsUnauthorized()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();
        (await _sut.Deactivate(10, default)).Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Deactivate_CommandSucceeds_ReturnsOk()
    {
        _commands.Setup(c => c.DispatchAsync(It.IsAny<DeactivateCharacterCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));

        (await _sut.Deactivate(10, default)).Should().BeOfType<OkObjectResult>();
    }
}
