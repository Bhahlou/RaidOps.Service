using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RaidOps.API.Controllers.v1;
using RaidOps.Application.Contracts.Characters.Responses;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Roster.Queries;
using RaidOps.Application.Contracts.Guilds.Roster.Responses;
using RaidOps.Domain.Enums;

namespace RaidOps.UnitTests.Controllers;

public class GuildRosterControllerTests
{
    private readonly Mock<ICommandDispatcher> _commands = new();
    private readonly Mock<IQueryDispatcher>   _queries  = new();
    private readonly GuildRosterController    _sut;

    private const string DiscordId     = "user-1";
    private const string GuildId       = "guild-1";
    private const int    GuildBranchId = 1;

    public GuildRosterControllerTests()
    {
        _sut = new GuildRosterController(_commands.Object, _queries.Object)
        {
            ControllerContext = ControllerTestHelpers.MakeContext(DiscordId)
        };
    }

    // ── GetRoster ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRoster_SubMissing_ReturnsUnauthorized()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();

        var result = await _sut.GetRoster(GuildId, GuildBranchId, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetRoster_QueryFails_ReturnsBadRequest()
    {
        _queries.Setup(q => q.DispatchAsync<GetGuildRosterQuery, List<GuildRosterMemberResponse>>(
                It.IsAny<GetGuildRosterQuery>(), default))
            .ReturnsAsync(Result<List<GuildRosterMemberResponse>>.Fail(ResponseDetail.RosterAccessDenied));

        var result = await _sut.GetRoster(GuildId, GuildBranchId, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetRoster_Success_ReturnsOkWithResponse()
    {
        var response = new List<GuildRosterMemberResponse>
        {
            new()
            {
                CharacterId = 1,
                CharacterName = "Arthas",
                ClassId = 6,
                ClassName = "Death Knight",
                ClassColor = "#C41F3B",
                Level = 80,
                BranchName = "Classic Anniversary",
                RealmSlug = "kazzak",
                PlayerDiscordId = DiscordId,
                RaidSpecs = [],
                CharacterRank = CharacterRank.Main,
                JoinedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), 
                CanExclude = true
            },
        };
        _queries.Setup(q => q.DispatchAsync<GetGuildRosterQuery, List<GuildRosterMemberResponse>>(
                It.IsAny<GetGuildRosterQuery>(), default))
            .ReturnsAsync(Result<List<GuildRosterMemberResponse>>.Ok(response));

        var result = await _sut.GetRoster(GuildId, GuildBranchId, default);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(response);
    }

    [Fact]
    public async Task GetRoster_PassesCorrectQueryFields()
    {
        _queries.Setup(q => q.DispatchAsync<GetGuildRosterQuery, List<GuildRosterMemberResponse>>(
                It.IsAny<GetGuildRosterQuery>(), default))
            .ReturnsAsync(Result<List<GuildRosterMemberResponse>>.Ok([])); // remplacer [] par new List<...>()

        await _sut.GetRoster(GuildId, GuildBranchId, default);

        _queries.Verify(q => q.DispatchAsync<GetGuildRosterQuery, List<GuildRosterMemberResponse>>(
            It.Is<GetGuildRosterQuery>(x => x.GuildId == GuildId && x.RequesterDiscordId == DiscordId),
            default), Times.Once);
    }
}
