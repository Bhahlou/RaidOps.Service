using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RaidOps.API.Controllers.v1;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Memberships.Commands;
using RaidOps.Application.Contracts.Guilds.Memberships.Queries;
using RaidOps.Application.Contracts.Guilds.Memberships.Responses;

namespace RaidOps.UnitTests.Controllers;

/// <summary>
/// Unit tests for <see cref="GuildMembershipController"/>.
/// Verifies routing of commands/queries to the correct dispatchers and claim extraction.
/// </summary>
public class GuildMembershipControllerTests
{
    private readonly Mock<ICommandDispatcher>    _commands = new();
    private readonly Mock<IQueryDispatcher>      _queries  = new();
    private readonly GuildMembershipController   _sut;

    private const string DiscordId   = "user-1";
    private const int    CharacterId = 10;
    private const string GuildId     = "guild-1";

    public GuildMembershipControllerTests()
    {
        _sut = new GuildMembershipController(_commands.Object, _queries.Object)
        {
            ControllerContext = ControllerTestHelpers.MakeContext(DiscordId)
        };
    }

    // ── GetEligibleGuilds ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetEligibleGuilds_SubMissing_ReturnsUnauthorized()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();
        (await _sut.GetEligibleGuilds(CharacterId, default)).Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetEligibleGuilds_QuerySucceeds_ReturnsOk()
    {
        _queries.Setup(q => q.DispatchAsync<GetEligibleGuildsQuery, List<EligibleGuildResponse>>(
                It.Is<GetEligibleGuildsQuery>(x =>
                    x.CharacterId == CharacterId && x.RequesterDiscordId == DiscordId), default))
            .ReturnsAsync(Result<List<EligibleGuildResponse>>.Ok([]));

        (await _sut.GetEligibleGuilds(CharacterId, default)).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetEligibleGuilds_QueryFails_ReturnsBadRequest()
    {
        _queries.Setup(q => q.DispatchAsync<GetEligibleGuildsQuery, List<EligibleGuildResponse>>(
                It.IsAny<GetEligibleGuildsQuery>(), default))
            .ReturnsAsync(Result<List<EligibleGuildResponse>>.Fail(ResponseDetail.CharacterNotFound));

        (await _sut.GetEligibleGuilds(CharacterId, default)).Should().BeOfType<BadRequestObjectResult>();
    }

    // ── JoinGuild ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task JoinGuild_SubMissing_ReturnsUnauthorized()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();
        (await _sut.JoinGuild(CharacterId, GuildId, new JoinGuildCommand(), default))
            .Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task JoinGuild_CommandSucceeds_ReturnsOk()
    {
        _commands.Setup(c => c.DispatchAsync(
                It.Is<JoinGuildCommand>(x =>
                    x.CharacterId == CharacterId &&
                    x.GuildId == GuildId &&
                    x.RequesterDiscordId == DiscordId), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));

        (await _sut.JoinGuild(CharacterId, GuildId, new JoinGuildCommand(), default))
            .Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task JoinGuild_CommandFails_ReturnsBadRequest()
    {
        _commands.Setup(c => c.DispatchAsync(It.IsAny<JoinGuildCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.AlreadyMember));

        (await _sut.JoinGuild(CharacterId, GuildId, new JoinGuildCommand(), default))
            .Should().BeOfType<BadRequestObjectResult>();
    }

    // ── UpdateCharacterRank ───────────────────────────────────────────────────

    [Fact]
    public async Task UpdateCharacterRank_SubMissing_ReturnsUnauthorized()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();
        (await _sut.UpdateCharacterRank(CharacterId, GuildId, new UpdateCharacterRankCommand(), default))
            .Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task UpdateCharacterRank_CommandSucceeds_ReturnsOk()
    {
        _commands.Setup(c => c.DispatchAsync(
                It.Is<UpdateCharacterRankCommand>(x =>
                    x.CharacterId == CharacterId &&
                    x.GuildId == GuildId &&
                    x.RequesterDiscordId == DiscordId), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));

        (await _sut.UpdateCharacterRank(CharacterId, GuildId, new UpdateCharacterRankCommand(), default))
            .Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdateCharacterRank_CommandFails_ReturnsBadRequest()
    {
        _commands.Setup(c => c.DispatchAsync(It.IsAny<UpdateCharacterRankCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.NotAMember));

        (await _sut.UpdateCharacterRank(CharacterId, GuildId, new UpdateCharacterRankCommand(), default))
            .Should().BeOfType<BadRequestObjectResult>();
    }

    // ── LeaveGuild ────────────────────────────────────────────────────────────

    [Fact]
    public async Task LeaveGuild_SubMissing_ReturnsUnauthorized()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();
        (await _sut.LeaveGuild(CharacterId, GuildId, default)).Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task LeaveGuild_CommandSucceeds_ReturnsOk()
    {
        _commands.Setup(c => c.DispatchAsync(
                It.Is<LeaveGuildCommand>(x =>
                    x.CharacterId == CharacterId &&
                    x.GuildId == GuildId &&
                    x.RequesterDiscordId == DiscordId), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));

        (await _sut.LeaveGuild(CharacterId, GuildId, default)).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task LeaveGuild_CommandFails_ReturnsBadRequest()
    {
        _commands.Setup(c => c.DispatchAsync(It.IsAny<LeaveGuildCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.NotAMember));

        (await _sut.LeaveGuild(CharacterId, GuildId, default)).Should().BeOfType<BadRequestObjectResult>();
    }

}
