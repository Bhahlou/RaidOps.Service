using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using RaidOps.API.Hubs;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using Xunit;

namespace RaidOps.UnitTests.Hubs;

public class RaidSignupHubTests
{
    private readonly Mock<IGuildAccessService> _guildAccessService = new();
    private readonly Mock<IGroupManager> _groups = new();
    private readonly Mock<HubCallerContext> _context = new();
    private readonly RaidSignupHub _sut;

    private const string ConnectionId = "conn-1";
    private const string DiscordId = "42";
    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const int EventId = 5;

    public RaidSignupHubTests()
    {
        _context.Setup(c => c.ConnectionId).Returns(ConnectionId);
        _sut = new RaidSignupHub(_guildAccessService.Object)
        {
            Context = _context.Object,
            Groups = _groups.Object,
        };
    }

    private void SetUser(string? discordId)
    {
        var claims = discordId is null ? Array.Empty<Claim>() : [new Claim(JwtRegisteredClaimNames.Sub, discordId)];
        _context.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity(claims, "test")));
    }

    // ── JoinRaidEvent ─────────────────────────────────────────────────────────

    [Fact]
    public async Task JoinRaidEvent_NoSubClaim_DoesNotJoinGroup()
    {
        SetUser(null);

        await _sut.JoinRaidEvent(GuildId, GuildBranchId, EventId);

        _groups.Verify(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
        _guildAccessService.Verify(a => a.GetAccessLevelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task JoinRaidEvent_NoUserPrincipal_DoesNotJoinGroup()
    {
        _context.Setup(c => c.User).Returns((System.Security.Claims.ClaimsPrincipal?)null!);

        await _sut.JoinRaidEvent(GuildId, GuildBranchId, EventId);

        _groups.Verify(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
        _guildAccessService.Verify(a => a.GetAccessLevelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task JoinRaidEvent_BelowRosterAccess_DoesNotJoinGroup()
    {
        SetUser(DiscordId);
        _guildAccessService.Setup(a => a.GetAccessLevelAsync(DiscordId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.None);

        await _sut.JoinRaidEvent(GuildId, GuildBranchId, EventId);

        _groups.Verify(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task JoinRaidEvent_RosterAccess_JoinsTheEventGroup()
    {
        SetUser(DiscordId);
        _guildAccessService.Setup(a => a.GetAccessLevelAsync(DiscordId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        await _sut.JoinRaidEvent(GuildId, GuildBranchId, EventId);

        _groups.Verify(g => g.AddToGroupAsync(ConnectionId, RaidSignupHub.GroupName(GuildBranchId, EventId), default), Times.Once);
    }

    [Fact]
    public async Task JoinRaidEvent_OfficerAccess_JoinsTheEventGroup()
    {
        SetUser(DiscordId);
        _guildAccessService.Setup(a => a.GetAccessLevelAsync(DiscordId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);

        await _sut.JoinRaidEvent(GuildId, GuildBranchId, EventId);

        _groups.Verify(g => g.AddToGroupAsync(ConnectionId, RaidSignupHub.GroupName(GuildBranchId, EventId), default), Times.Once);
    }

    // ── LeaveRaidEvent ────────────────────────────────────────────────────────

    [Fact]
    public async Task LeaveRaidEvent_RemovesFromTheEventGroup()
    {
        await _sut.LeaveRaidEvent(GuildBranchId, EventId);

        _groups.Verify(g => g.RemoveFromGroupAsync(ConnectionId, RaidSignupHub.GroupName(GuildBranchId, EventId), default), Times.Once);
    }

    // ── GroupName ─────────────────────────────────────────────────────────────

    [Fact]
    public void GroupName_CombinesBranchAndEventId()
    {
        RaidSignupHub.GroupName(GuildBranchId, EventId).Should().Be("raid-signup:10:5");
    }
}
