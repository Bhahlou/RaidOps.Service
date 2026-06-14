using FluentAssertions;
using Moq;
using NetCord;
using NetCord.Gateway;
using RaidOps.Application.Contracts.Common;
using DiscordGuild = RaidOps.Domain.Models.Discord.Guild;
using RaidOps.Application.Contracts.Guilds.Memberships.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Guilds.Memberships.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Discord;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;
using RaidOps.UnitTests.ExternalApplication.Bot;

namespace RaidOps.UnitTests.Application.Guilds.Memberships.CommandHandlers;

/// <summary>
/// Unit tests for <see cref="JoinGuildCommandHandler"/>.
/// </summary>
public class JoinGuildCommandHandlerTests
{
    private readonly Mock<ICharacterRepository>       _characters  = new();
    private readonly Mock<IGuildsRepository>          _guilds      = new();
    private readonly Mock<IUserGuildsRepository>      _userGuilds  = new();
    private readonly Mock<IGuildMembershipRepository> _memberships = new();
    private readonly Mock<IDiscordBotService>         _bot         = new();
    private readonly Mock<IGuildService>              _guild       = new();
    private readonly Mock<IAuditLogService>           _auditLog    = new();
    private readonly JoinGuildCommandHandler          _sut;

    private const int    CharacterId   = 1;
    private const string GuildId       = "guild-1";
    private const string DiscordId     = "200000000000000001";
    private const ulong  DiscordUlong  = 200000000000000001UL;
    private const string MinRoleId     = "100000000000000001";
    private const ulong  MinRoleUlong  = 100000000000000001UL;
    private const ulong  LowRoleUlong  = 100000000000000002UL;

    private static readonly JoinGuildCommand Command = new()
    {
        CharacterId        = CharacterId,
        GuildId            = GuildId,
        RequesterDiscordId = DiscordId,
        CharacterRank      = CharacterRank.Main,
    };

    public JoinGuildCommandHandlerTests()
    {
        _bot.Setup(b => b.Guilds).Returns(_guild.Object);
        _sut = new JoinGuildCommandHandler(
            _characters.Object,
            _guilds.Object,
            _userGuilds.Object,
            _memberships.Object,
            _bot.Object,
            _auditLog.Object);
    }

    // ── CharacterNotFound ─────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_CharacterNotFound_ReturnsCharacterNotFound()
    {
        _characters.Setup(r => r.GetByIdAsync(CharacterId, default)).ReturnsAsync((Character?)null);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.CharacterNotFound);
    }

    // ── CharacterNotOwned ─────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_CharacterNotOwned_ReturnsCharacterNotOwned()
    {
        _characters.Setup(r => r.GetByIdAsync(CharacterId, default))
            .ReturnsAsync(new Character { Id = CharacterId, Name = "Arthas", UserDiscordId = "other-user" });

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.CharacterNotOwned);
    }

    // ── GuildNotFound ─────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_GuildNotFound_ReturnsGuildNotFound()
    {
        SetupCharacter();
        _guilds.Setup(r => r.GetByIdAsync(GuildId, default)).ReturnsAsync((DiscordGuild?)null);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildNotFound);
    }

    // ── GuildNotRegistered ────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_GuildNotRegistered_ReturnsGuildNotRegistered()
    {
        SetupCharacter();
        _guilds.Setup(r => r.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild { Id = GuildId, Name = "RaidOps", IsRegistered = false });

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildNotRegistered);
    }

    // ── GuildNotConfigured (RosterMode null) ──────────────────────────────

    [Fact]
    public async Task HandleAsync_RosterModeNull_ReturnsGuildNotConfigured()
    {
        SetupCharacter();
        _guilds.Setup(r => r.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild { Id = GuildId, Name = "RaidOps", IsRegistered = true, RosterMode = null });

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildNotConfigured);
    }

    // ── NotDiscordMember ──────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_NotDiscordMember_ReturnsForbidden()
    {
        SetupCharacter();
        SetupOpenGuild();
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    // ── Open — AlreadyMember ──────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_Open_AlreadyMember_ReturnsAlreadyMember()
    {
        SetupCharacter();
        SetupOpenGuild();
        SetupDiscordMember();
        _memberships.Setup(r => r.ExistsAsync(CharacterId, GuildId, default)).ReturnsAsync(true);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.AlreadyMember);
    }

    // ── Open — Success ────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_Open_Success_AddsAndLogs()
    {
        SetupCharacter();
        SetupOpenGuild();
        SetupDiscordMember();
        _memberships.Setup(r => r.ExistsAsync(CharacterId, GuildId, default)).ReturnsAsync(false);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _memberships.Verify(r => r.AddAsync(
            It.Is<GuildMembership>(m => m.CharacterId == CharacterId && m.GuildId == GuildId &&
                                        m.CharacterRank == CharacterRank.Main),
            default), Times.Once);
        _auditLog.Verify(a => a.LogAsync(
            GuildId, DiscordId, GuildAuditAction.MemberJoined,
            It.IsAny<Dictionary<string, string>?>(), default), Times.Once);
    }

    // ── DiscordRoleOnly — MinRosterRoleId null ────────────────────────────

    [Fact]
    public async Task HandleAsync_DiscordRoleOnly_MinRoleIdNull_ReturnsGuildNotConfigured()
    {
        SetupCharacter();
        _guilds.Setup(r => r.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild
            {
                Id = GuildId, Name = "RaidOps", IsRegistered = true,
                RosterMode = RosterMode.DiscordRoleOnly, MinRosterRoleId = null,
            });
        SetupDiscordMember();

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildNotConfigured);
    }

    // ── DiscordRoleOnly — BotNotPresent ───────────────────────────────────

    [Fact]
    public async Task HandleAsync_DiscordRoleOnly_BotNotPresent_ReturnsBotNotPresent()
    {
        SetupCharacter();
        SetupRoleOnlyGuild();
        SetupDiscordMember();
        _guild.Setup(g => g.GetRoles(GuildId, default)).Throws<InvalidOperationException>();

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildBotNotPresent);
    }

    // ── DiscordRoleOnly — MinRole not in Discord ──────────────────────────

    [Fact]
    public async Task HandleAsync_DiscordRoleOnly_MinRoleNotFound_ReturnsRosterAccessDenied()
    {
        SetupCharacter();
        SetupRoleOnlyGuild();
        SetupDiscordMember();

        // Only role present is 999 — not MinRoleId
        var otherRole = NetCordTestHelpers.MakeJsonRole(999UL, (Permissions)0, position: 5);
        var g = NetCordTestHelpers.MakeGuild(0UL, 0UL, new Dictionary<ulong, GuildUser>(), [otherRole]);
        _guild.Setup(gs => gs.GetRoles(GuildId, default)).Returns(g.Roles.Values);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RosterAccessDenied);
    }

    // ── DiscordRoleOnly — User not in Discord guild ───────────────────────

    [Fact]
    public async Task HandleAsync_DiscordRoleOnly_UserNotInDiscordGuild_ReturnsRosterAccessDenied()
    {
        SetupCharacter();
        SetupRoleOnlyGuild();
        SetupDiscordMember();
        SetupMinRole(position: 5);
        _guild.Setup(gs => gs.GetUsers(GuildId, default)).Returns([]);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RosterAccessDenied);
    }

    // ── DiscordRoleOnly — User has insufficient role ───────────────────────

    [Fact]
    public async Task HandleAsync_DiscordRoleOnly_UserLacksRole_ReturnsRosterAccessDenied()
    {
        SetupCharacter();
        SetupRoleOnlyGuild();
        SetupDiscordMember();

        // MinRole at position 10; user's role at position 3
        var minJson = NetCordTestHelpers.MakeJsonRole(MinRoleUlong, (Permissions)0, position: 10);
        var lowJson = NetCordTestHelpers.MakeJsonRole(LowRoleUlong, (Permissions)0, position: 3);
        var g = NetCordTestHelpers.MakeGuild(0UL, 0UL, new Dictionary<ulong, GuildUser>(), [minJson, lowJson]);
        _guild.Setup(gs => gs.GetRoles(GuildId, default)).Returns(g.Roles.Values);

        var guildUser = NetCordTestHelpers.MakeGuildUser(DiscordUlong, 0UL, [LowRoleUlong]);
        _guild.Setup(gs => gs.GetUsers(GuildId, default)).Returns([guildUser]);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RosterAccessDenied);
    }

    // ── DiscordRoleOnly — AlreadyMember ───────────────────────────────────

    [Fact]
    public async Task HandleAsync_DiscordRoleOnly_AlreadyMember_ReturnsAlreadyMember()
    {
        SetupCharacter();
        SetupRoleOnlyGuild();
        SetupDiscordMember();
        SetupRoleAccess();
        _memberships.Setup(r => r.ExistsAsync(CharacterId, GuildId, default)).ReturnsAsync(true);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.AlreadyMember);
    }

    // ── DiscordRoleOnly — Success ─────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_DiscordRoleOnly_Success_AddsAndLogs()
    {
        SetupCharacter();
        SetupRoleOnlyGuild();
        SetupDiscordMember();
        SetupRoleAccess();
        _memberships.Setup(r => r.ExistsAsync(CharacterId, GuildId, default)).ReturnsAsync(false);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _memberships.Verify(r => r.AddAsync(
            It.Is<GuildMembership>(m => m.CharacterId == CharacterId && m.GuildId == GuildId),
            default), Times.Once);
        _auditLog.Verify(a => a.LogAsync(
            GuildId, DiscordId, GuildAuditAction.MemberJoined,
            It.IsAny<Dictionary<string, string>?>(), default), Times.Once);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void SetupCharacter() =>
        _characters.Setup(r => r.GetByIdAsync(CharacterId, default))
            .ReturnsAsync(new Character { Id = CharacterId, Name = "Arthas", UserDiscordId = DiscordId });

    private void SetupOpenGuild() =>
        _guilds.Setup(r => r.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild { Id = GuildId, Name = "RaidOps", IsRegistered = true, RosterMode = RosterMode.Open });

    private void SetupRoleOnlyGuild() =>
        _guilds.Setup(r => r.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild
            {
                Id = GuildId, Name = "RaidOps", IsRegistered = true,
                RosterMode = RosterMode.DiscordRoleOnly, MinRosterRoleId = MinRoleId,
            });

    private void SetupDiscordMember() =>
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId }]);

    private void SetupMinRole(int position)
    {
        var jsonRole = NetCordTestHelpers.MakeJsonRole(MinRoleUlong, (Permissions)0, position: position);
        var g = NetCordTestHelpers.MakeGuild(0UL, 0UL, new Dictionary<ulong, GuildUser>(), [jsonRole]);
        _guild.Setup(gs => gs.GetRoles(GuildId, default)).Returns(g.Roles.Values);
    }

    private void SetupRoleAccess()
    {
        // User has MinRole itself — position equal to threshold
        var jsonRole  = NetCordTestHelpers.MakeJsonRole(MinRoleUlong, (Permissions)0, position: 5);
        var g = NetCordTestHelpers.MakeGuild(0UL, 0UL, new Dictionary<ulong, GuildUser>(), [jsonRole]);
        _guild.Setup(gs => gs.GetRoles(GuildId, default)).Returns(g.Roles.Values);

        var guildUser = NetCordTestHelpers.MakeGuildUser(DiscordUlong, 0UL, [MinRoleUlong]);
        _guild.Setup(gs => gs.GetUsers(GuildId, default)).Returns([guildUser]);
    }
}
