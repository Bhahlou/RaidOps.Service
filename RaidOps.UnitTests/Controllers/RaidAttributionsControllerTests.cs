using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RaidOps.API.Controllers.v1;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Attributions.Commands;
using RaidOps.Application.Contracts.Raids.Attributions.Queries;
using RaidOps.Application.Contracts.Raids.Attributions.Responses;

namespace RaidOps.UnitTests.Controllers;

public class RaidAttributionsControllerTests
{
    private readonly Mock<ICommandDispatcher> _commands = new();
    private readonly Mock<IQueryDispatcher> _queries = new();
    private RaidAttributionsController MakeSut(string? discordId = "user-1") => new(_commands.Object, _queries.Object)
    {
        ControllerContext = discordId is null ? ControllerTestHelpers.MakeAnonymousContext() : ControllerTestHelpers.MakeContext(discordId),
    };

    // ── GetAttributions ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetAttributions_NoDiscordId_ReturnsUnauthorized()
    {
        var result = await MakeSut(null).GetAttributions("guild-1", 7, 42, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetAttributions_QuerySucceeds_ReturnsOk()
    {
        var response = new RaidEventAttributionsResponse { Definitions = [], Fills = [], SeatedCharacters = [] };
        _queries.Setup(q => q.DispatchAsync<GetRaidEventAttributionsQuery, RaidEventAttributionsResponse>(
                It.Is<GetRaidEventAttributionsQuery>(qr => qr.GuildId == "guild-1" && qr.GuildBranchId == 7 && qr.EventId == 42 && qr.RequesterDiscordId == "user-1"), default))
            .ReturnsAsync(Result<RaidEventAttributionsResponse>.Ok(response));

        var result = await MakeSut().GetAttributions("guild-1", 7, 42, default);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(response);
    }

    [Fact]
    public async Task GetAttributions_QueryFails_ReturnsBadRequest()
    {
        _queries.Setup(q => q.DispatchAsync<GetRaidEventAttributionsQuery, RaidEventAttributionsResponse>(It.IsAny<GetRaidEventAttributionsQuery>(), default))
            .ReturnsAsync(Result<RaidEventAttributionsResponse>.Fail(ResponseDetail.Forbidden));

        var result = await MakeSut().GetAttributions("guild-1", 7, 42, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── SetAttribution ───────────────────────────────────────────────────────

    [Fact]
    public async Task SetAttribution_NoDiscordId_ReturnsUnauthorized()
    {
        var command = new SetRaidEventAttributionCommand { DefinitionId = 1, CellId = 2, CharacterId = 100 };

        var result = await MakeSut(null).SetAttribution("guild-1", 7, 42, command, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task SetAttribution_SetsRouteAndClaimFields()
    {
        SetRaidEventAttributionCommand? dispatched = null;
        _commands.Setup(c => c.DispatchAsync(It.IsAny<SetRaidEventAttributionCommand>(), default))
            .Callback<SetRaidEventAttributionCommand, CancellationToken>((c, _) => dispatched = c)
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));
        var command = new SetRaidEventAttributionCommand { DefinitionId = 1, CellId = 2, InstanceIndex = 3, CharacterId = 100 };

        var result = await MakeSut("user-1").SetAttribution("guild-1", 7, 42, command, default);

        result.Should().BeOfType<OkObjectResult>();
        dispatched.Should().NotBeNull();
        dispatched!.GuildId.Should().Be("guild-1");
        dispatched.GuildBranchId.Should().Be(7);
        dispatched.EventId.Should().Be(42);
        dispatched.RequesterDiscordId.Should().Be("user-1");
        dispatched.CharacterId.Should().Be(100);
    }

    [Fact]
    public async Task SetAttribution_CommandFails_ReturnsBadRequest()
    {
        _commands.Setup(c => c.DispatchAsync(It.IsAny<SetRaidEventAttributionCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.CharacterNotSeatedInEvent));
        var command = new SetRaidEventAttributionCommand { DefinitionId = 1, CellId = 2, CharacterId = 100 };

        var result = await MakeSut().SetAttribution("guild-1", 7, 42, command, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── ClearAttribution ─────────────────────────────────────────────────────

    [Fact]
    public async Task ClearAttribution_NoDiscordId_ReturnsUnauthorized()
    {
        var command = new ClearRaidEventAttributionCommand { DefinitionId = 1, CellId = 2 };

        var result = await MakeSut(null).ClearAttribution("guild-1", 7, 42, command, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task ClearAttribution_SetsRouteAndClaimFields()
    {
        ClearRaidEventAttributionCommand? dispatched = null;
        _commands.Setup(c => c.DispatchAsync(It.IsAny<ClearRaidEventAttributionCommand>(), default))
            .Callback<ClearRaidEventAttributionCommand, CancellationToken>((c, _) => dispatched = c)
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));
        var command = new ClearRaidEventAttributionCommand { DefinitionId = 1, CellId = 2, InstanceIndex = 3 };

        var result = await MakeSut("user-1").ClearAttribution("guild-1", 7, 42, command, default);

        result.Should().BeOfType<OkObjectResult>();
        dispatched.Should().NotBeNull();
        dispatched!.GuildId.Should().Be("guild-1");
        dispatched.GuildBranchId.Should().Be(7);
        dispatched.EventId.Should().Be(42);
        dispatched.RequesterDiscordId.Should().Be("user-1");
    }

    [Fact]
    public async Task ClearAttribution_CommandFails_ReturnsBadRequest()
    {
        _commands.Setup(c => c.DispatchAsync(It.IsAny<ClearRaidEventAttributionCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.SlotEmpty));
        var command = new ClearRaidEventAttributionCommand { DefinitionId = 1, CellId = 2 };

        var result = await MakeSut().ClearAttribution("guild-1", 7, 42, command, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
