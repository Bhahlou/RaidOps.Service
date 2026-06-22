using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RaidOps.API.Controllers.v1;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.AuditLog.Queries;
using RaidOps.Application.Contracts.Guilds.AuditLog.Responses;
using RaidOps.Domain.Enums;

namespace RaidOps.UnitTests.Controllers;

public class GuildAuditLogControllerTests
{
    private readonly Mock<ICommandDispatcher> _commands = new();
    private readonly Mock<IQueryDispatcher>   _queries  = new();
    private readonly GuildAuditLogController  _sut;

    private const string DiscordId = "user-1";
    private const string GuildId   = "guild-1";

    public GuildAuditLogControllerTests()
    {
        _sut = new GuildAuditLogController(_commands.Object, _queries.Object)
        {
            ControllerContext = ControllerTestHelpers.MakeContext(DiscordId)
        };
    }

    // ── GetAuditLog ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAuditLog_SubMissing_ReturnsUnauthorized()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();

        var result = await _sut.GetAuditLog(GuildId);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetAuditLog_QueryFails_ReturnsBadRequest()
    {
        _queries.Setup(q => q.DispatchAsync<GetGuildAuditLogQuery, GuildAuditLogPageResponse>(
                It.IsAny<GetGuildAuditLogQuery>(), default))
            .ReturnsAsync(Result<GuildAuditLogPageResponse>.Fail(ResponseDetail.Forbidden));

        var result = await _sut.GetAuditLog(GuildId);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetAuditLog_Success_ReturnsOkWithResponse()
    {
        var response = new GuildAuditLogPageResponse { Entries = [], HasMore = false };
        _queries.Setup(q => q.DispatchAsync<GetGuildAuditLogQuery, GuildAuditLogPageResponse>(
                It.IsAny<GetGuildAuditLogQuery>(), default))
            .ReturnsAsync(Result<GuildAuditLogPageResponse>.Ok(response));

        var result = await _sut.GetAuditLog(GuildId);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(response);
    }

    [Fact]
    public async Task GetAuditLog_DefaultParams_PassesDefaultsAndNullFilters()
    {
        _queries.Setup(q => q.DispatchAsync<GetGuildAuditLogQuery, GuildAuditLogPageResponse>(
                It.IsAny<GetGuildAuditLogQuery>(), default))
            .ReturnsAsync(Result<GuildAuditLogPageResponse>.Ok(new GuildAuditLogPageResponse { Entries = [], HasMore = false }));

        await _sut.GetAuditLog(GuildId);

        _queries.Verify(q => q.DispatchAsync<GetGuildAuditLogQuery, GuildAuditLogPageResponse>(
            It.Is<GetGuildAuditLogQuery>(x =>
                x.GuildId == GuildId && x.RequesterDiscordId == DiscordId &&
                x.Page == 1 && x.PageSize == 25 &&
                x.ActionType == null && x.Category == null),
            default), Times.Once);
    }

    [Fact]
    public async Task GetAuditLog_PassesCorrectQueryFields()
    {
        _queries.Setup(q => q.DispatchAsync<GetGuildAuditLogQuery, GuildAuditLogPageResponse>(
                It.IsAny<GetGuildAuditLogQuery>(), default))
            .ReturnsAsync(Result<GuildAuditLogPageResponse>.Ok(new GuildAuditLogPageResponse { Entries = [], HasMore = false }));

        await _sut.GetAuditLog(GuildId, page: 2, pageSize: 10, actionType: GuildAuditAction.MemberJoined, category: GuildAuditCategory.Roster);

        _queries.Verify(q => q.DispatchAsync<GetGuildAuditLogQuery, GuildAuditLogPageResponse>(
            It.Is<GetGuildAuditLogQuery>(x =>
                x.GuildId == GuildId && x.RequesterDiscordId == DiscordId &&
                x.Page == 2 && x.PageSize == 10 &&
                x.ActionType == GuildAuditAction.MemberJoined && x.Category == GuildAuditCategory.Roster),
            default), Times.Once);
    }
}
