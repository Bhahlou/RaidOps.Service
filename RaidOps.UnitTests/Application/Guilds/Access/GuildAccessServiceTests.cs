using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NetCord;
using NetCord.Gateway;
using DiscordGuild = RaidOps.Domain.Models.Discord.Guild;
using RaidOps.Application.Implementations.Guilds.Access;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;
using RaidOps.UnitTests.ExternalApplication.Bot;

namespace RaidOps.UnitTests.Application.Guilds.Access;

/// <summary>
/// Unit tests for <see cref="GuildAccessService"/>.
/// </summary>
public class GuildAccessServiceTests
{
    private readonly Mock<IUserGuildsRepository>     _userGuilds    = new();
    private readonly Mock<IGuildsRepository>         _guilds        = new();
    private readonly Mock<IGuildBranchesRepository>  _guildBranches = new();
    private readonly Mock<IDiscordBotService>        _bot           = new();
    private readonly Mock<IGuildService>             _guildService  = new();
    private readonly GuildAccessService              _sut;

    private const string GuildId       = "guild-1";
    private const string DiscordId     = "200000000000000001";
    private const ulong  DiscordUlong  = 200000000000000001UL;
    private const string RosterRoleId  = "100000000000000001";
    private const ulong  RosterRoleUlong = 100000000000000001UL;
    private const string OfficerRoleId = "400000000000000001";
    private const ulong  OfficerRoleUlong = 400000000000000001UL;
    private const string TargetDiscordId = "250000000000000001";
    private const ulong  TargetDiscordUlong = 250000000000000001UL;
    private const ulong  RequesterRoleUlong = 410000000000000001UL;
    private const ulong  TargetRoleUlong = 420000000000000001UL;
    private const int    GuildBranchId = 1;
    private const int    BranchGameId  = 10;

    public GuildAccessServiceTests()
    {
        _bot.Setup(b => b.Guilds).Returns(_guildService.Object);
        _sut = new GuildAccessService(_userGuilds.Object, _guilds.Object, _guildBranches.Object, _bot.Object, NullLogger<GuildAccessService>.Instance);
    }

    private static GuildBranch MakeBranch(
        RosterMode? rosterMode = null,
        List<string>? rosterRoleIds = null,
        List<string>? officerRoleIds = null,
        bool isActive = true,
        string guildId = GuildId)
        => new()
        {
            Id = GuildBranchId,
            GuildId = guildId,
            BranchId = BranchGameId,
            RosterMode = rosterMode,
            RosterRoleIds = rosterRoleIds ?? [],
            OfficerRoleIds = officerRoleIds ?? [],
            IsActive = isActive,
        };

    // ── GetAccessLevelAsync (guild-wide) ──────────────────────────────────────

    [Fact]
    public async Task GetAccessLevelAsync_NoMembership_ReturnsNone()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default)).ReturnsAsync([]);

        var result = await _sut.GetAccessLevelAsync(DiscordId, GuildId, default);

        result.Should().Be(GuildAccessLevel.None);
    }

    [Fact]
    public async Task GetAccessLevelAsync_AdminMembership_ReturnsOfficer()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = true }]);

        var result = await _sut.GetAccessLevelAsync(DiscordId, GuildId, default);

        result.Should().Be(GuildAccessLevel.Officer);
        _guilds.Verify(g => g.GetByIdAsync(It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task GetAccessLevelAsync_GuildNotFound_ReturnsNone()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false }]);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync((DiscordGuild?)null);

        var result = await _sut.GetAccessLevelAsync(DiscordId, GuildId, default);

        result.Should().Be(GuildAccessLevel.None);
    }

    [Fact]
    public async Task GetAccessLevelAsync_GuildNotRegistered_ReturnsNone()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false }]);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild { Id = GuildId, Name = "RaidOps", IsRegistered = false });

        var result = await _sut.GetAccessLevelAsync(DiscordId, GuildId, default);

        result.Should().Be(GuildAccessLevel.None);
    }

    [Fact]
    public async Task GetAccessLevelAsync_NoActiveBranches_ReturnsPublic()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false }]);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild { Id = GuildId, Name = "RaidOps", IsRegistered = true });
        _guildBranches.Setup(b => b.GetActiveForGuildAsync(GuildId, default)).ReturnsAsync([]);

        var result = await _sut.GetAccessLevelAsync(DiscordId, GuildId, default);

        result.Should().Be(GuildAccessLevel.Public);
    }

    [Fact]
    public async Task GetAccessLevelAsync_ActiveBranchRosterModeOpen_ReturnsRoster()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false }]);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild { Id = GuildId, Name = "RaidOps", IsRegistered = true });
        _guildBranches.Setup(b => b.GetActiveForGuildAsync(GuildId, default))
            .ReturnsAsync([MakeBranch(rosterMode: RosterMode.Open)]);

        var result = await _sut.GetAccessLevelAsync(DiscordId, GuildId, default);

        result.Should().Be(GuildAccessLevel.Roster);
    }

    [Fact]
    public async Task GetAccessLevelAsync_ActiveBranchNotConfigured_ReturnsPublic()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false }]);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild { Id = GuildId, Name = "RaidOps", IsRegistered = true });
        _guildBranches.Setup(b => b.GetActiveForGuildAsync(GuildId, default))
            .ReturnsAsync([MakeBranch(rosterMode: null)]);

        var result = await _sut.GetAccessLevelAsync(DiscordId, GuildId, default);

        result.Should().Be(GuildAccessLevel.Public);
    }

    [Fact]
    public async Task GetAccessLevelAsync_MultipleActiveBranches_ReturnsMaxAccessLevel()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false }]);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild { Id = GuildId, Name = "RaidOps", IsRegistered = true });

        var publicOnlyBranch = MakeBranch(rosterMode: null);
        var officerBranch = new GuildBranch
        {
            Id = 2, GuildId = GuildId, BranchId = 20,
            OfficerRoleIds = [OfficerRoleId], RosterRoleIds = [], IsActive = true,
        };
        _guildBranches.Setup(b => b.GetActiveForGuildAsync(GuildId, default))
            .ReturnsAsync([publicOnlyBranch, officerBranch]);

        _guildService.Setup(g => g.GetUsers(GuildId, default))
            .Returns([NetCordTestHelpers.MakeGuildUser(DiscordUlong, 0UL, [OfficerRoleUlong])]);

        var result = await _sut.GetAccessLevelAsync(DiscordId, GuildId, default);

        result.Should().Be(GuildAccessLevel.Officer);
    }

    [Fact]
    public async Task GetAccessLevelAsync_DiscordRoleOnly_UserHasRole_ReturnsRoster()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false }]);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild { Id = GuildId, Name = "RaidOps", IsRegistered = true });
        _guildBranches.Setup(b => b.GetActiveForGuildAsync(GuildId, default))
            .ReturnsAsync([MakeBranch(rosterMode: RosterMode.DiscordRoleOnly, rosterRoleIds: [RosterRoleId])]);

        var guildUser = NetCordTestHelpers.MakeGuildUser(DiscordUlong, 0UL, [RosterRoleUlong]);
        _guildService.Setup(g => g.GetUsers(GuildId, default)).Returns([guildUser]);

        var result = await _sut.GetAccessLevelAsync(DiscordId, GuildId, default);

        result.Should().Be(GuildAccessLevel.Roster);
    }

    [Fact]
    public async Task GetAccessLevelAsync_DiscordRoleOnly_UserLacksRole_ReturnsPublic()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false }]);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild { Id = GuildId, Name = "RaidOps", IsRegistered = true });
        _guildBranches.Setup(b => b.GetActiveForGuildAsync(GuildId, default))
            .ReturnsAsync([MakeBranch(rosterMode: RosterMode.DiscordRoleOnly, rosterRoleIds: [RosterRoleId])]);
        _guildService.Setup(g => g.GetUsers(GuildId, default)).Returns([]);

        var result = await _sut.GetAccessLevelAsync(DiscordId, GuildId, default);

        result.Should().Be(GuildAccessLevel.Public);
    }

    [Fact]
    public async Task GetAccessLevelAsync_DiscordRoleOnly_RosterRoleIdsEmpty_ReturnsPublicWithoutQueryingBot()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false }]);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild { Id = GuildId, Name = "RaidOps", IsRegistered = true });
        _guildBranches.Setup(b => b.GetActiveForGuildAsync(GuildId, default))
            .ReturnsAsync([MakeBranch(rosterMode: RosterMode.DiscordRoleOnly, rosterRoleIds: [])]);

        var result = await _sut.GetAccessLevelAsync(DiscordId, GuildId, default);

        result.Should().Be(GuildAccessLevel.Public);
        _guildService.Verify(g => g.GetUsers(It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task GetAccessLevelAsync_DiscordRoleOnly_BotNotPresent_ReturnsPublic()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false }]);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild { Id = GuildId, Name = "RaidOps", IsRegistered = true });
        _guildBranches.Setup(b => b.GetActiveForGuildAsync(GuildId, default))
            .ReturnsAsync([MakeBranch(rosterMode: RosterMode.DiscordRoleOnly, rosterRoleIds: [RosterRoleId])]);
        _guildService.Setup(g => g.GetUsers(GuildId, default)).Throws<InvalidOperationException>();

        var result = await _sut.GetAccessLevelAsync(DiscordId, GuildId, default);

        result.Should().Be(GuildAccessLevel.Public);
    }

    // ── GetAccessLevelAsync (branch-scoped) ───────────────────────────────────

    [Fact]
    public async Task GetAccessLevelAsyncBranch_NoMembership_ReturnsNone()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default)).ReturnsAsync([]);

        var result = await _sut.GetAccessLevelAsync(DiscordId, GuildId, GuildBranchId, default);

        result.Should().Be(GuildAccessLevel.None);
    }

    [Fact]
    public async Task GetAccessLevelAsyncBranch_AdminMembership_ReturnsOfficerWithoutBranchLookup()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = true }]);

        var result = await _sut.GetAccessLevelAsync(DiscordId, GuildId, GuildBranchId, default);

        result.Should().Be(GuildAccessLevel.Officer);
        _guildBranches.Verify(b => b.GetByIdAsync(It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task GetAccessLevelAsyncBranch_BranchNotFound_ReturnsNone()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false }]);
        _guildBranches.Setup(b => b.GetByIdAsync(GuildBranchId, default)).ReturnsAsync((GuildBranch?)null);

        var result = await _sut.GetAccessLevelAsync(DiscordId, GuildId, GuildBranchId, default);

        result.Should().Be(GuildAccessLevel.None);
    }

    [Fact]
    public async Task GetAccessLevelAsyncBranch_BranchBelongsToDifferentGuild_ReturnsNone()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false }]);
        _guildBranches.Setup(b => b.GetByIdAsync(GuildBranchId, default))
            .ReturnsAsync(MakeBranch(rosterMode: RosterMode.Open, guildId: "other-guild"));

        var result = await _sut.GetAccessLevelAsync(DiscordId, GuildId, GuildBranchId, default);

        result.Should().Be(GuildAccessLevel.None);
    }

    [Fact]
    public async Task GetAccessLevelAsyncBranch_BranchInactive_ReturnsNone()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false }]);
        _guildBranches.Setup(b => b.GetByIdAsync(GuildBranchId, default))
            .ReturnsAsync(MakeBranch(rosterMode: RosterMode.Open, isActive: false));

        var result = await _sut.GetAccessLevelAsync(DiscordId, GuildId, GuildBranchId, default);

        result.Should().Be(GuildAccessLevel.None);
    }

    [Fact]
    public async Task GetAccessLevelAsyncBranch_GuildNotRegistered_ReturnsNone()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false }]);
        _guildBranches.Setup(b => b.GetByIdAsync(GuildBranchId, default))
            .ReturnsAsync(MakeBranch(rosterMode: RosterMode.Open));
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild { Id = GuildId, Name = "RaidOps", IsRegistered = false });

        var result = await _sut.GetAccessLevelAsync(DiscordId, GuildId, GuildBranchId, default);

        result.Should().Be(GuildAccessLevel.None);
    }

    [Fact]
    public async Task GetAccessLevelAsyncBranch_RosterModeOpen_ReturnsRoster()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false }]);
        _guildBranches.Setup(b => b.GetByIdAsync(GuildBranchId, default))
            .ReturnsAsync(MakeBranch(rosterMode: RosterMode.Open));
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild { Id = GuildId, Name = "RaidOps", IsRegistered = true });

        var result = await _sut.GetAccessLevelAsync(DiscordId, GuildId, GuildBranchId, default);

        result.Should().Be(GuildAccessLevel.Roster);
    }

    [Fact]
    public async Task GetAccessLevelAsyncBranch_OfficerRoleMatch_ReturnsOfficer()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false }]);
        _guildBranches.Setup(b => b.GetByIdAsync(GuildBranchId, default))
            .ReturnsAsync(MakeBranch(rosterMode: RosterMode.Open, officerRoleIds: [OfficerRoleId]));
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild { Id = GuildId, Name = "RaidOps", IsRegistered = true });
        _guildService.Setup(g => g.GetUsers(GuildId, default))
            .Returns([NetCordTestHelpers.MakeGuildUser(DiscordUlong, 0UL, [OfficerRoleUlong])]);

        var result = await _sut.GetAccessLevelAsync(DiscordId, GuildId, GuildBranchId, default);

        result.Should().Be(GuildAccessLevel.Officer);
    }

    // ── ComputeAccessLevel ───────────────────────────────────────────────────

    [Fact]
    public void ComputeAccessLevel_AdminMembership_ReturnsOfficer()
    {
        var membership = new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = true };
        var branch = MakeBranch();

        var result = _sut.ComputeAccessLevel(membership, branch, default);

        result.Should().Be(GuildAccessLevel.Officer);
    }

    [Fact]
    public void ComputeAccessLevel_RosterModeOpen_ReturnsRoster()
    {
        var membership = new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false };
        var branch = MakeBranch(rosterMode: RosterMode.Open);

        var result = _sut.ComputeAccessLevel(membership, branch, default);

        result.Should().Be(GuildAccessLevel.Roster);
    }

    [Fact]
    public void ComputeAccessLevel_RosterModeNull_ReturnsPublic()
    {
        var membership = new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false };
        var branch = MakeBranch(rosterMode: null);

        var result = _sut.ComputeAccessLevel(membership, branch, default);

        result.Should().Be(GuildAccessLevel.Public);
    }

    [Fact]
    public void ComputeAccessLevel_OfficerRoleIdsContainsUserRole_ReturnsOfficer()
    {
        var membership = new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false };
        var branch = MakeBranch(rosterMode: RosterMode.Open, officerRoleIds: [OfficerRoleId]);
        _guildService.Setup(g => g.GetUsers(GuildId, default))
            .Returns([NetCordTestHelpers.MakeGuildUser(DiscordUlong, 0UL, [OfficerRoleUlong])]);

        var result = _sut.ComputeAccessLevel(membership, branch, default);

        result.Should().Be(GuildAccessLevel.Officer);
    }

    [Fact]
    public void ComputeAccessLevel_OfficerRoleIdsSet_UserHoldsOneOfSeveral_ReturnsOfficer()
    {
        const string otherOfficerRoleId = "450000000000000001";
        var membership = new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false };
        var branch = MakeBranch(rosterMode: RosterMode.Open, officerRoleIds: [OfficerRoleId, otherOfficerRoleId]);
        _guildService.Setup(g => g.GetUsers(GuildId, default))
            .Returns([NetCordTestHelpers.MakeGuildUser(DiscordUlong, 0UL, [OfficerRoleUlong])]);

        var result = _sut.ComputeAccessLevel(membership, branch, default);

        result.Should().Be(GuildAccessLevel.Officer);
    }

    [Fact]
    public void ComputeAccessLevel_UserHoldsMultipleRolesOneMatchesOfficerSet_ReturnsOfficer()
    {
        const ulong otherHeldRoleUlong = 460000000000000001UL;
        var membership = new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false };
        var branch = MakeBranch(rosterMode: RosterMode.Open, officerRoleIds: [OfficerRoleId]);
        _guildService.Setup(g => g.GetUsers(GuildId, default))
            .Returns([NetCordTestHelpers.MakeGuildUser(DiscordUlong, 0UL, [otherHeldRoleUlong, OfficerRoleUlong])]);

        var result = _sut.ComputeAccessLevel(membership, branch, default);

        result.Should().Be(GuildAccessLevel.Officer);
    }

    [Fact]
    public void ComputeAccessLevel_OfficerRoleIdsSet_UserLacksAny_FallsThroughToRosterCheck()
    {
        var membership = new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false };
        var branch = MakeBranch(rosterMode: RosterMode.Open, officerRoleIds: [OfficerRoleId]);
        _guildService.Setup(g => g.GetUsers(GuildId, default))
            .Returns([NetCordTestHelpers.MakeGuildUser(DiscordUlong, 0UL, [])]);

        var result = _sut.ComputeAccessLevel(membership, branch, default);

        result.Should().Be(GuildAccessLevel.Roster);
    }

    [Fact]
    public void ComputeAccessLevel_OfficerRoleIdsEmpty_SkipsBotCallForOfficerCheck()
    {
        var membership = new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false };
        var branch = MakeBranch(rosterMode: RosterMode.Open, officerRoleIds: []);

        var result = _sut.ComputeAccessLevel(membership, branch, default);

        result.Should().Be(GuildAccessLevel.Roster);
        _guildService.Verify(g => g.GetUsers(It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public void ComputeAccessLevel_OfficerCheckBotNotPresent_FallsThroughToRoster()
    {
        var membership = new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false };
        var branch = MakeBranch(rosterMode: RosterMode.Open, officerRoleIds: [OfficerRoleId]);
        _guildService.Setup(g => g.GetUsers(GuildId, default)).Throws<InvalidOperationException>();

        var result = _sut.ComputeAccessLevel(membership, branch, default);

        result.Should().Be(GuildAccessLevel.Roster);
    }

    [Fact]
    public void ComputeAccessLevel_RosterModeDiscordRoleOnly_RosterRoleIdsMatch_ReturnsRoster()
    {
        var membership = new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false };
        var branch = MakeBranch(rosterMode: RosterMode.DiscordRoleOnly, rosterRoleIds: [RosterRoleId]);
        _guildService.Setup(g => g.GetUsers(GuildId, default))
            .Returns([NetCordTestHelpers.MakeGuildUser(DiscordUlong, 0UL, [RosterRoleUlong])]);

        var result = _sut.ComputeAccessLevel(membership, branch, default);

        result.Should().Be(GuildAccessLevel.Roster);
    }

    [Fact]
    public void ComputeAccessLevel_RosterModeDiscordRoleOnly_RosterRoleIdsEmpty_ReturnsPublic()
    {
        var membership = new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false };
        var branch = MakeBranch(rosterMode: RosterMode.DiscordRoleOnly, rosterRoleIds: []);

        var result = _sut.ComputeAccessLevel(membership, branch, default);

        result.Should().Be(GuildAccessLevel.Public);
    }

    // ── OutranksAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task OutranksAsync_RequesterIsAdmin_TargetNotOwner_ReturnsTrue()
    {
        // The owner check runs first and needs the target's membership regardless of the
        // requester's admin status, so the target lookup always happens — just not the owner gate.
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = true }]);
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(TargetDiscordId, default)).ReturnsAsync([]);

        var result = await _sut.OutranksAsync(GuildId, GuildBranchId, DiscordId, TargetDiscordId, default);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task OutranksAsync_TargetIsOwner_ReturnsFalseEvenIfRequesterIsAdmin()
    {
        // Bundled owner-outrank fix: nobody, not even another admin, can act on the real owner.
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = true }]);
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(TargetDiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = TargetDiscordId, IsAdmin = true, IsOwner = true }]);

        var result = await _sut.OutranksAsync(GuildId, GuildBranchId, DiscordId, TargetDiscordId, default);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task OutranksAsync_TargetIsAdmin_RequesterNotAdmin_ReturnsFalse()
    {
        SetupNonAdminMemberships();
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(TargetDiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = TargetDiscordId, IsAdmin = true }]);

        var result = await _sut.OutranksAsync(GuildId, GuildBranchId, DiscordId, TargetDiscordId, default);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task OutranksAsync_RequesterHasNoMembershipRow_FallsThroughToRoleComparison()
    {
        // No UserGuilds row at all for the requester (e.g. never synced) — the `?.IsAdmin == true`
        // null-conditional must resolve to false (not throw, not short-circuit as admin) and fall
        // through to the role-position comparison.
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default)).ReturnsAsync([]);
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(TargetDiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = TargetDiscordId, IsAdmin = false }]);
        SetupRoles(requesterPosition: 10, targetPosition: 5);

        var result = await _sut.OutranksAsync(GuildId, GuildBranchId, DiscordId, TargetDiscordId, default);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task OutranksAsync_TargetHasNoMembershipRow_FallsThroughToRoleComparison()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false }]);
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(TargetDiscordId, default)).ReturnsAsync([]);
        SetupRoles(requesterPosition: 10, targetPosition: 5);

        var result = await _sut.OutranksAsync(GuildId, GuildBranchId, DiscordId, TargetDiscordId, default);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task OutranksAsync_BothAdmins_RequesterOutranks()
    {
        // The requester's admin short-circuit fires before the target is even inspected —
        // an admin can act on anyone, including another admin (but never the owner, see above).
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = true }]);
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(TargetDiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = TargetDiscordId, IsAdmin = true }]);

        var result = await _sut.OutranksAsync(GuildId, GuildBranchId, DiscordId, TargetDiscordId, default);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task OutranksAsync_RequesterHasHigherRolePosition_ReturnsTrue()
    {
        SetupNonAdminMemberships();
        SetupRoles(requesterPosition: 10, targetPosition: 5);

        var result = await _sut.OutranksAsync(GuildId, GuildBranchId, DiscordId, TargetDiscordId, default);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task OutranksAsync_RequesterHasLowerRolePosition_ReturnsFalse()
    {
        SetupNonAdminMemberships();
        SetupRoles(requesterPosition: 5, targetPosition: 10);

        var result = await _sut.OutranksAsync(GuildId, GuildBranchId, DiscordId, TargetDiscordId, default);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task OutranksAsync_SameRole_ReturnsFalse()
    {
        // Real Discord role positions are unique per role, so a genuine tie only occurs when both
        // members hold the exact same role — not two different roles that happen to share a
        // position value (NetCord's RolePosition breaks such ties by role ID for a stable order).
        SetupNonAdminMemberships();
        var sharedRole = NetCordTestHelpers.MakeJsonRole(RequesterRoleUlong, (Permissions)0, position: 5);
        var netcordGuild = NetCordTestHelpers.MakeGuild(0UL, 0UL, new Dictionary<ulong, GuildUser>(), [sharedRole]);
        _guildService.Setup(g => g.GetRoles(GuildId, default)).Returns(netcordGuild.Roles.Values);
        _guildService.Setup(g => g.GetUsers(GuildId, default)).Returns([
            NetCordTestHelpers.MakeGuildUser(DiscordUlong, 0UL, [RequesterRoleUlong]),
            NetCordTestHelpers.MakeGuildUser(TargetDiscordUlong, 0UL, [RequesterRoleUlong]),
        ]);

        var result = await _sut.OutranksAsync(GuildId, GuildBranchId, DiscordId, TargetDiscordId, default);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task OutranksAsync_TargetHasNoRoles_ReturnsTrue()
    {
        SetupNonAdminMemberships();
        var requesterRole = NetCordTestHelpers.MakeJsonRole(RequesterRoleUlong, (Permissions)0, position: 1);
        var netcordGuild = NetCordTestHelpers.MakeGuild(0UL, 0UL, new Dictionary<ulong, GuildUser>(), [requesterRole]);
        _guildService.Setup(g => g.GetRoles(GuildId, default)).Returns(netcordGuild.Roles.Values);
        _guildService.Setup(g => g.GetUsers(GuildId, default)).Returns([
            NetCordTestHelpers.MakeGuildUser(DiscordUlong, 0UL, [RequesterRoleUlong]),
            // Target is present but holds no roles at all.
            NetCordTestHelpers.MakeGuildUser(TargetDiscordUlong, 0UL, []),
        ]);

        var result = await _sut.OutranksAsync(GuildId, GuildBranchId, DiscordId, TargetDiscordId, default);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task OutranksAsync_RequesterHasNoRoles_ReturnsFalse()
    {
        SetupNonAdminMemberships();
        var targetRole = NetCordTestHelpers.MakeJsonRole(TargetRoleUlong, (Permissions)0, position: 1);
        var netcordGuild = NetCordTestHelpers.MakeGuild(0UL, 0UL, new Dictionary<ulong, GuildUser>(), [targetRole]);
        _guildService.Setup(g => g.GetRoles(GuildId, default)).Returns(netcordGuild.Roles.Values);
        _guildService.Setup(g => g.GetUsers(GuildId, default)).Returns([
            NetCordTestHelpers.MakeGuildUser(DiscordUlong, 0UL, []),
            NetCordTestHelpers.MakeGuildUser(TargetDiscordUlong, 0UL, [TargetRoleUlong]),
        ]);

        var result = await _sut.OutranksAsync(GuildId, GuildBranchId, DiscordId, TargetDiscordId, default);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task OutranksAsync_BotNotPresent_ReturnsFalse()
    {
        SetupNonAdminMemberships();
        _guildService.Setup(g => g.GetRoles(GuildId, default)).Throws<InvalidOperationException>();

        var result = await _sut.OutranksAsync(GuildId, GuildBranchId, DiscordId, TargetDiscordId, default);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task OutranksAsync_RequesterNotInGuildMemberList_ReturnsFalse()
    {
        // The requester's Discord account left the server (or was never a member) — GetUsers
        // simply doesn't return them, distinct from being present with zero roles.
        SetupNonAdminMemberships();
        var targetRole = NetCordTestHelpers.MakeJsonRole(TargetRoleUlong, (Permissions)0, position: 1);
        var netcordGuild = NetCordTestHelpers.MakeGuild(0UL, 0UL, new Dictionary<ulong, GuildUser>(), [targetRole]);
        _guildService.Setup(g => g.GetRoles(GuildId, default)).Returns(netcordGuild.Roles.Values);
        _guildService.Setup(g => g.GetUsers(GuildId, default)).Returns([
            NetCordTestHelpers.MakeGuildUser(TargetDiscordUlong, 0UL, [TargetRoleUlong]),
        ]);

        var result = await _sut.OutranksAsync(GuildId, GuildBranchId, DiscordId, TargetDiscordId, default);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task OutranksAsync_RequesterHasMultipleRoles_PicksHighestValidPosition()
    {
        // Exercises the full max-finding loop: a stale role id no longer in the roles dictionary
        // (skipped), a high-position role (becomes the running max), then a lower-position role
        // (must NOT overwrite the running max).
        const ulong staleRoleUlong = 430000000000000001UL;
        const ulong lowRoleUlong = 440000000000000001UL;
        SetupNonAdminMemberships();
        var highRole = NetCordTestHelpers.MakeJsonRole(RequesterRoleUlong, (Permissions)0, position: 9);
        var lowRole = NetCordTestHelpers.MakeJsonRole(lowRoleUlong, (Permissions)0, position: 3);
        var targetRole = NetCordTestHelpers.MakeJsonRole(TargetRoleUlong, (Permissions)0, position: 5);
        var netcordGuild = NetCordTestHelpers.MakeGuild(0UL, 0UL, new Dictionary<ulong, GuildUser>(), [highRole, lowRole, targetRole]);
        _guildService.Setup(g => g.GetRoles(GuildId, default)).Returns(netcordGuild.Roles.Values);
        _guildService.Setup(g => g.GetUsers(GuildId, default)).Returns([
            NetCordTestHelpers.MakeGuildUser(DiscordUlong, 0UL, [staleRoleUlong, RequesterRoleUlong, lowRoleUlong]),
            NetCordTestHelpers.MakeGuildUser(TargetDiscordUlong, 0UL, [TargetRoleUlong]),
        ]);

        // Requester's highest valid position (9) beats the target's (5).
        var result = await _sut.OutranksAsync(GuildId, GuildBranchId, DiscordId, TargetDiscordId, default);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task OutranksAsync_BranchOfficer_OutranksNonOfficerRegardlessOfPosition()
    {
        // The requester holds one of the branch's Officer roles, the target doesn't — officer
        // status decides it even though the target's raw Discord role position is higher.
        SetupNonAdminMemberships();
        _guildBranches.Setup(b => b.GetByIdAsync(GuildBranchId, default))
            .ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId, BranchId = BranchGameId, OfficerRoleIds = [OfficerRoleId] });
        SetupRoles(requesterPosition: 1, targetPosition: 20);
        _guildService.Setup(g => g.GetUsers(GuildId, default)).Returns([
            NetCordTestHelpers.MakeGuildUser(DiscordUlong, 0UL, [RequesterRoleUlong, OfficerRoleUlong]),
            NetCordTestHelpers.MakeGuildUser(TargetDiscordUlong, 0UL, [TargetRoleUlong]),
        ]);

        var result = await _sut.OutranksAsync(GuildId, GuildBranchId, DiscordId, TargetDiscordId, default);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task OutranksAsync_BranchNonOfficer_OutrankedByOfficerRegardlessOfPosition()
    {
        SetupNonAdminMemberships();
        _guildBranches.Setup(b => b.GetByIdAsync(GuildBranchId, default))
            .ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId, BranchId = BranchGameId, OfficerRoleIds = [OfficerRoleId] });
        SetupRoles(requesterPosition: 20, targetPosition: 1);
        _guildService.Setup(g => g.GetUsers(GuildId, default)).Returns([
            NetCordTestHelpers.MakeGuildUser(DiscordUlong, 0UL, [RequesterRoleUlong]),
            NetCordTestHelpers.MakeGuildUser(TargetDiscordUlong, 0UL, [TargetRoleUlong, OfficerRoleUlong]),
        ]);

        var result = await _sut.OutranksAsync(GuildId, GuildBranchId, DiscordId, TargetDiscordId, default);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task OutranksAsync_BranchBothOfficers_FallsBackToPositionComparison()
    {
        SetupNonAdminMemberships();
        _guildBranches.Setup(b => b.GetByIdAsync(GuildBranchId, default))
            .ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId, BranchId = BranchGameId, OfficerRoleIds = [OfficerRoleId] });
        SetupRoles(requesterPosition: 10, targetPosition: 5);
        _guildService.Setup(g => g.GetUsers(GuildId, default)).Returns([
            NetCordTestHelpers.MakeGuildUser(DiscordUlong, 0UL, [RequesterRoleUlong, OfficerRoleUlong]),
            NetCordTestHelpers.MakeGuildUser(TargetDiscordUlong, 0UL, [TargetRoleUlong, OfficerRoleUlong]),
        ]);

        var result = await _sut.OutranksAsync(GuildId, GuildBranchId, DiscordId, TargetDiscordId, default);

        result.Should().BeTrue();
    }

    private void SetupNonAdminMemberships()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false }]);
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(TargetDiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = TargetDiscordId, IsAdmin = false }]);
    }

    private void SetupRoles(int requesterPosition, int targetPosition)
    {
        var requesterRole = NetCordTestHelpers.MakeJsonRole(RequesterRoleUlong, (Permissions)0, position: requesterPosition);
        var targetRole = NetCordTestHelpers.MakeJsonRole(TargetRoleUlong, (Permissions)0, position: targetPosition);
        var netcordGuild = NetCordTestHelpers.MakeGuild(0UL, 0UL, new Dictionary<ulong, GuildUser>(), [requesterRole, targetRole]);
        _guildService.Setup(g => g.GetRoles(GuildId, default)).Returns(netcordGuild.Roles.Values);
        _guildService.Setup(g => g.GetUsers(GuildId, default)).Returns([
            NetCordTestHelpers.MakeGuildUser(DiscordUlong, 0UL, [RequesterRoleUlong]),
            NetCordTestHelpers.MakeGuildUser(TargetDiscordUlong, 0UL, [TargetRoleUlong]),
        ]);
    }
}
