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
    private readonly Mock<IUserGuildsRepository> _userGuilds   = new();
    private readonly Mock<IGuildsRepository>     _guilds       = new();
    private readonly Mock<IDiscordBotService>    _bot          = new();
    private readonly Mock<IGuildService>         _guildService = new();
    private readonly GuildAccessService          _sut;

    private const string GuildId      = "guild-1";
    private const string DiscordId    = "200000000000000001";
    private const ulong  DiscordUlong = 200000000000000001UL;
    private const string MinRoleId    = "100000000000000001";
    private const ulong  MinRoleUlong = 100000000000000001UL;
    private const string OfficerRoleId = "400000000000000001";
    private const ulong  OfficerRoleUlong = 400000000000000001UL;
    private const string TargetDiscordId = "250000000000000001";
    private const ulong  TargetDiscordUlong = 250000000000000001UL;
    private const ulong  RequesterRoleUlong = 410000000000000001UL;
    private const ulong  TargetRoleUlong = 420000000000000001UL;

    public GuildAccessServiceTests()
    {
        _bot.Setup(b => b.Guilds).Returns(_guildService.Object);
        _sut = new GuildAccessService(_userGuilds.Object, _guilds.Object, _bot.Object, NullLogger<GuildAccessService>.Instance);
    }

    // ── GetAccessLevelAsync ──────────────────────────────────────────────────

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
    public async Task GetAccessLevelAsync_RosterModeOpen_ReturnsRoster()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false }]);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild { Id = GuildId, Name = "RaidOps", IsRegistered = true, RosterMode = RosterMode.Open });

        var result = await _sut.GetAccessLevelAsync(DiscordId, GuildId, default);

        result.Should().Be(GuildAccessLevel.Roster);
    }

    [Fact]
    public async Task GetAccessLevelAsync_RosterModeNull_ReturnsPublic()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false }]);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild { Id = GuildId, Name = "RaidOps", IsRegistered = true, RosterMode = null });

        var result = await _sut.GetAccessLevelAsync(DiscordId, GuildId, default);

        result.Should().Be(GuildAccessLevel.Public);
    }

    [Fact]
    public async Task GetAccessLevelAsync_DiscordRoleOnly_UserHasRole_ReturnsRoster()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false }]);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild
            {
                Id = GuildId, Name = "RaidOps", IsRegistered = true,
                RosterMode = RosterMode.DiscordRoleOnly, MinRosterRoleId = MinRoleId,
            });

        var jsonRole = NetCordTestHelpers.MakeJsonRole(MinRoleUlong, (Permissions)0, position: 5);
        var netcordGuild = NetCordTestHelpers.MakeGuild(0UL, 0UL, new Dictionary<ulong, GuildUser>(), [jsonRole]);
        _guildService.Setup(g => g.GetRoles(GuildId, default)).Returns(netcordGuild.Roles.Values);

        var guildUser = NetCordTestHelpers.MakeGuildUser(DiscordUlong, 0UL, [MinRoleUlong]);
        _guildService.Setup(g => g.GetUsers(GuildId, default)).Returns([guildUser]);

        var result = await _sut.GetAccessLevelAsync(DiscordId, GuildId, default);

        result.Should().Be(GuildAccessLevel.Roster);
    }

    [Fact]
    public async Task GetAccessLevelAsync_DiscordRoleOnly_UserHasLowerRole_ReturnsPublic()
    {
        const ulong lowerRoleUlong = 300000000000000001UL;
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false }]);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild
            {
                Id = GuildId, Name = "RaidOps", IsRegistered = true,
                RosterMode = RosterMode.DiscordRoleOnly, MinRosterRoleId = MinRoleId,
            });

        // The user holds a real role below the configured threshold's position.
        var minRole = NetCordTestHelpers.MakeJsonRole(MinRoleUlong, (Permissions)0, position: 5);
        var lowerRole = NetCordTestHelpers.MakeJsonRole(lowerRoleUlong, (Permissions)0, position: 2);
        var netcordGuild = NetCordTestHelpers.MakeGuild(0UL, 0UL, new Dictionary<ulong, GuildUser>(), [minRole, lowerRole]);
        _guildService.Setup(g => g.GetRoles(GuildId, default)).Returns(netcordGuild.Roles.Values);

        var guildUser = NetCordTestHelpers.MakeGuildUser(DiscordUlong, 0UL, [lowerRoleUlong]);
        _guildService.Setup(g => g.GetUsers(GuildId, default)).Returns([guildUser]);

        var result = await _sut.GetAccessLevelAsync(DiscordId, GuildId, default);

        result.Should().Be(GuildAccessLevel.Public);
    }

    [Fact]
    public async Task GetAccessLevelAsync_DiscordRoleOnly_UserLacksRole_ReturnsPublic()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false }]);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild
            {
                Id = GuildId, Name = "RaidOps", IsRegistered = true,
                RosterMode = RosterMode.DiscordRoleOnly, MinRosterRoleId = MinRoleId,
            });
        _guildService.Setup(g => g.GetUsers(GuildId, default)).Returns([]);

        var jsonRole = NetCordTestHelpers.MakeJsonRole(MinRoleUlong, (Permissions)0, position: 5);
        var netcordGuild = NetCordTestHelpers.MakeGuild(0UL, 0UL, new Dictionary<ulong, GuildUser>(), [jsonRole]);
        _guildService.Setup(g => g.GetRoles(GuildId, default)).Returns(netcordGuild.Roles.Values);

        var result = await _sut.GetAccessLevelAsync(DiscordId, GuildId, default);

        result.Should().Be(GuildAccessLevel.Public);
    }

    [Fact]
    public async Task GetAccessLevelAsync_DiscordRoleOnly_MinRosterRoleIdNull_ReturnsPublicWithoutQueryingBot()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false }]);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild
            {
                Id = GuildId, Name = "RaidOps", IsRegistered = true,
                RosterMode = RosterMode.DiscordRoleOnly, MinRosterRoleId = null,
            });

        var result = await _sut.GetAccessLevelAsync(DiscordId, GuildId, default);

        result.Should().Be(GuildAccessLevel.Public);
        _guildService.Verify(g => g.GetRoles(It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task GetAccessLevelAsync_DiscordRoleOnly_ConfiguredRoleNoLongerExists_ReturnsPublic()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false }]);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild
            {
                Id = GuildId, Name = "RaidOps", IsRegistered = true,
                RosterMode = RosterMode.DiscordRoleOnly, MinRosterRoleId = MinRoleId,
            });
        // The configured role was deleted from the Discord server — GetRoles no longer returns it.
        var netcordGuild = NetCordTestHelpers.MakeGuild(0UL, 0UL, new Dictionary<ulong, GuildUser>(), []);
        _guildService.Setup(g => g.GetRoles(GuildId, default)).Returns(netcordGuild.Roles.Values);

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
            .ReturnsAsync(new DiscordGuild
            {
                Id = GuildId, Name = "RaidOps", IsRegistered = true,
                RosterMode = RosterMode.DiscordRoleOnly, MinRosterRoleId = MinRoleId,
            });
        _guildService.Setup(g => g.GetRoles(GuildId, default)).Throws<InvalidOperationException>();

        var result = await _sut.GetAccessLevelAsync(DiscordId, GuildId, default);

        result.Should().Be(GuildAccessLevel.Public);
    }

    // ── ComputeAccessLevel ───────────────────────────────────────────────────

    [Fact]
    public void ComputeAccessLevel_AdminMembership_ReturnsOfficer()
    {
        var membership = new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = true };
        var guild = new DiscordGuild { Id = GuildId, Name = "RaidOps", IsRegistered = false };

        var result = _sut.ComputeAccessLevel(membership, guild, default);

        result.Should().Be(GuildAccessLevel.Officer);
    }

    [Fact]
    public void ComputeAccessLevel_GuildNotRegistered_ReturnsNone()
    {
        var membership = new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false };
        var guild = new DiscordGuild { Id = GuildId, Name = "RaidOps", IsRegistered = false };

        var result = _sut.ComputeAccessLevel(membership, guild, default);

        result.Should().Be(GuildAccessLevel.None);
    }

    [Fact]
    public void ComputeAccessLevel_RosterModeOpen_ReturnsRoster()
    {
        var membership = new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false };
        var guild = new DiscordGuild { Id = GuildId, Name = "RaidOps", IsRegistered = true, RosterMode = RosterMode.Open };

        var result = _sut.ComputeAccessLevel(membership, guild, default);

        result.Should().Be(GuildAccessLevel.Roster);
    }

    // ── ComputeAccessLevel — Officer threshold ────────────────────────────────

    [Fact]
    public void ComputeAccessLevel_OfficerThresholdMatch_ReturnsOfficer()
    {
        var membership = new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false };
        var guild = new DiscordGuild
        {
            Id = GuildId, Name = "RaidOps", IsRegistered = true,
            RosterMode = RosterMode.Open, MinOfficerRoleId = OfficerRoleId,
        };
        var officerRole = NetCordTestHelpers.MakeJsonRole(OfficerRoleUlong, (Permissions)0, position: 5);
        var netcordGuild = NetCordTestHelpers.MakeGuild(0UL, 0UL, new Dictionary<ulong, GuildUser>(), [officerRole]);
        _guildService.Setup(g => g.GetRoles(GuildId, default)).Returns(netcordGuild.Roles.Values);
        var guildUser = NetCordTestHelpers.MakeGuildUser(DiscordUlong, 0UL, [OfficerRoleUlong]);
        _guildService.Setup(g => g.GetUsers(GuildId, default)).Returns([guildUser]);

        var result = _sut.ComputeAccessLevel(membership, guild, default);

        result.Should().Be(GuildAccessLevel.Officer);
    }

    [Fact]
    public void ComputeAccessLevel_OfficerThresholdUserHasHigherRole_ReturnsOfficer()
    {
        var membership = new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false };
        var guild = new DiscordGuild
        {
            Id = GuildId, Name = "RaidOps", IsRegistered = true,
            RosterMode = RosterMode.Open, MinOfficerRoleId = OfficerRoleId,
        };
        const ulong higherRoleUlong = 500000000000000001UL;
        var officerRole = NetCordTestHelpers.MakeJsonRole(OfficerRoleUlong, (Permissions)0, position: 5);
        var higherRole = NetCordTestHelpers.MakeJsonRole(higherRoleUlong, (Permissions)0, position: 8);
        var netcordGuild = NetCordTestHelpers.MakeGuild(0UL, 0UL, new Dictionary<ulong, GuildUser>(), [officerRole, higherRole]);
        _guildService.Setup(g => g.GetRoles(GuildId, default)).Returns(netcordGuild.Roles.Values);
        var guildUser = NetCordTestHelpers.MakeGuildUser(DiscordUlong, 0UL, [higherRoleUlong]);
        _guildService.Setup(g => g.GetUsers(GuildId, default)).Returns([guildUser]);

        var result = _sut.ComputeAccessLevel(membership, guild, default);

        result.Should().Be(GuildAccessLevel.Officer);
    }

    [Fact]
    public void ComputeAccessLevel_OfficerThresholdUserHasLowerRole_FallsThroughToRoster()
    {
        var membership = new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false };
        var guild = new DiscordGuild
        {
            Id = GuildId, Name = "RaidOps", IsRegistered = true,
            RosterMode = RosterMode.Open, MinOfficerRoleId = OfficerRoleId,
        };
        const ulong lowerRoleUlong = 600000000000000001UL;
        var officerRole = NetCordTestHelpers.MakeJsonRole(OfficerRoleUlong, (Permissions)0, position: 5);
        var lowerRole = NetCordTestHelpers.MakeJsonRole(lowerRoleUlong, (Permissions)0, position: 2);
        var netcordGuild = NetCordTestHelpers.MakeGuild(0UL, 0UL, new Dictionary<ulong, GuildUser>(), [officerRole, lowerRole]);
        _guildService.Setup(g => g.GetRoles(GuildId, default)).Returns(netcordGuild.Roles.Values);
        var guildUser = NetCordTestHelpers.MakeGuildUser(DiscordUlong, 0UL, [lowerRoleUlong]);
        _guildService.Setup(g => g.GetUsers(GuildId, default)).Returns([guildUser]);

        var result = _sut.ComputeAccessLevel(membership, guild, default);

        result.Should().Be(GuildAccessLevel.Roster);
    }

    [Fact]
    public void ComputeAccessLevel_MinOfficerRoleIdNull_SkipsBotCallForOfficerCheck()
    {
        var membership = new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false };
        var guild = new DiscordGuild
        {
            Id = GuildId, Name = "RaidOps", IsRegistered = true,
            RosterMode = RosterMode.Open, MinOfficerRoleId = null,
        };

        var result = _sut.ComputeAccessLevel(membership, guild, default);

        result.Should().Be(GuildAccessLevel.Roster);
        _guildService.Verify(g => g.GetRoles(It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public void ComputeAccessLevel_OfficerThresholdBotNotPresent_FallsThroughToRoster()
    {
        var membership = new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = false };
        var guild = new DiscordGuild
        {
            Id = GuildId, Name = "RaidOps", IsRegistered = true,
            RosterMode = RosterMode.Open, MinOfficerRoleId = OfficerRoleId,
        };
        _guildService.Setup(g => g.GetRoles(GuildId, default)).Throws<InvalidOperationException>();

        var result = _sut.ComputeAccessLevel(membership, guild, default);

        result.Should().Be(GuildAccessLevel.Roster);
    }

    // ── OutranksAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task OutranksAsync_RequesterIsAdmin_ReturnsTrueWithoutCheckingTarget()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = true }]);

        var result = await _sut.OutranksAsync(GuildId, DiscordId, TargetDiscordId, default);

        result.Should().BeTrue();
        _userGuilds.Verify(r => r.GetByUserDiscordIdAsync(TargetDiscordId, default), Times.Never);
    }

    [Fact]
    public async Task OutranksAsync_TargetIsAdmin_RequesterNotAdmin_ReturnsFalse()
    {
        SetupNonAdminMemberships();
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(TargetDiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = TargetDiscordId, IsAdmin = true }]);

        var result = await _sut.OutranksAsync(GuildId, DiscordId, TargetDiscordId, default);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task OutranksAsync_BothAdmins_RequesterOutranks()
    {
        // The requester's admin short-circuit fires before the target is even inspected —
        // an admin can act on anyone, including another admin.
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId, IsAdmin = true }]);

        var result = await _sut.OutranksAsync(GuildId, DiscordId, TargetDiscordId, default);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task OutranksAsync_RequesterHasHigherRolePosition_ReturnsTrue()
    {
        SetupNonAdminMemberships();
        SetupRoles(requesterPosition: 10, targetPosition: 5);

        var result = await _sut.OutranksAsync(GuildId, DiscordId, TargetDiscordId, default);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task OutranksAsync_RequesterHasLowerRolePosition_ReturnsFalse()
    {
        SetupNonAdminMemberships();
        SetupRoles(requesterPosition: 5, targetPosition: 10);

        var result = await _sut.OutranksAsync(GuildId, DiscordId, TargetDiscordId, default);

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

        var result = await _sut.OutranksAsync(GuildId, DiscordId, TargetDiscordId, default);

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

        var result = await _sut.OutranksAsync(GuildId, DiscordId, TargetDiscordId, default);

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

        var result = await _sut.OutranksAsync(GuildId, DiscordId, TargetDiscordId, default);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task OutranksAsync_BotNotPresent_ReturnsFalse()
    {
        SetupNonAdminMemberships();
        _guildService.Setup(g => g.GetRoles(GuildId, default)).Throws<InvalidOperationException>();

        var result = await _sut.OutranksAsync(GuildId, DiscordId, TargetDiscordId, default);

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

        var result = await _sut.OutranksAsync(GuildId, DiscordId, TargetDiscordId, default);

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
        var result = await _sut.OutranksAsync(GuildId, DiscordId, TargetDiscordId, default);

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
