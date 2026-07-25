using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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
    private readonly Mock<ICharacterRepository>       _characters    = new();
    private readonly Mock<IGuildsRepository>          _guilds        = new();
    private readonly Mock<IGuildBranchesRepository>   _guildBranches = new();
    private readonly Mock<IUserGuildsRepository>      _userGuilds    = new();
    private readonly Mock<IGuildMembershipRepository> _memberships   = new();
    private readonly Mock<IDiscordBotService>         _bot           = new();
    private readonly Mock<IGuildService>              _guild         = new();
    private readonly Mock<IAuditLogService>           _auditLog      = new();
    private readonly JoinGuildCommandHandler          _sut;

    private const int    CharacterId    = 1;
    private const string GuildId        = "guild-1";
    private const string DiscordId      = "200000000000000001";
    private const ulong  DiscordUlong   = 200000000000000001UL;
    private const string RosterRoleId   = "100000000000000001";
    private const ulong  RosterRoleUlong = 100000000000000001UL;
    private const int    CharacterBranchId = 10;
    private const int    GuildBranchId  = 1;

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
            _guildBranches.Object,
            _userGuilds.Object,
            _memberships.Object,
            _bot.Object,
            _auditLog.Object,
            NullLogger<JoinGuildCommandHandler>.Instance);
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

    // ── Branch not active on this guild ───────────────────────────────────

    [Fact]
    public async Task HandleAsync_BranchNotActiveOnGuild_ReturnsGuildBranchNotActive()
    {
        SetupCharacter();
        SetupRegisteredGuild();
        _guildBranches.Setup(b => b.GetByGuildAndBranchAsync(GuildId, CharacterBranchId, default))
            .ReturnsAsync((GuildBranch?)null);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildBranchNotActive);
    }

    [Fact]
    public async Task HandleAsync_BranchDeactivated_ReturnsGuildBranchNotActive()
    {
        SetupCharacter();
        SetupRegisteredGuild();
        _guildBranches.Setup(b => b.GetByGuildAndBranchAsync(GuildId, CharacterBranchId, default))
            .ReturnsAsync(MakeBranch(rosterMode: RosterMode.Open, isActive: false));

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildBranchNotActive);
    }

    // ── GuildNotConfigured (branch RosterMode null) ───────────────────────

    [Fact]
    public async Task HandleAsync_RosterModeNull_ReturnsGuildNotConfigured()
    {
        SetupCharacter();
        SetupRegisteredGuild();
        _guildBranches.Setup(b => b.GetByGuildAndBranchAsync(GuildId, CharacterBranchId, default))
            .ReturnsAsync(MakeBranch(rosterMode: null));

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
                                        m.GuildBranchId == GuildBranchId && m.CharacterRank == CharacterRank.Main),
            default), Times.Once);
        _auditLog.Verify(a => a.LogAsync(
            GuildId, DiscordId, GuildAuditAction.MemberJoined,
            It.Is<Dictionary<string, string>>(v => v["characterName"] == "Arthas" && v["characterClassId"] == "5"),
            default), Times.Once);
    }

    // ── DiscordRoleOnly — RosterRoleIds empty ─────────────────────────────

    [Fact]
    public async Task HandleAsync_DiscordRoleOnly_RosterRoleIdsEmpty_ReturnsGuildNotConfigured()
    {
        SetupCharacter();
        SetupRegisteredGuild();
        _guildBranches.Setup(b => b.GetByGuildAndBranchAsync(GuildId, CharacterBranchId, default))
            .ReturnsAsync(MakeBranch(rosterMode: RosterMode.DiscordRoleOnly, rosterRoleIds: []));
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
        _guild.Setup(g => g.GetUsers(GuildId, default)).Throws<InvalidOperationException>();

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildBotNotPresent);
    }

    // ── DiscordRoleOnly — User not in Discord guild ───────────────────────

    [Fact]
    public async Task HandleAsync_DiscordRoleOnly_UserNotInDiscordGuild_ReturnsRosterAccessDenied()
    {
        SetupCharacter();
        SetupRoleOnlyGuild();
        SetupDiscordMember();
        _guild.Setup(gs => gs.GetUsers(GuildId, default)).Returns([]);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RosterAccessDenied);
    }

    // ── DiscordRoleOnly — User has none of the roster roles ────────────────

    [Fact]
    public async Task HandleAsync_DiscordRoleOnly_UserLacksRole_ReturnsRosterAccessDenied()
    {
        SetupCharacter();
        SetupRoleOnlyGuild();
        SetupDiscordMember();

        const ulong unrelatedRoleUlong = 999999999999999999UL;
        var guildUser = NetCordTestHelpers.MakeGuildUser(DiscordUlong, 0UL, [unrelatedRoleUlong]);
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
            It.Is<GuildMembership>(m => m.CharacterId == CharacterId && m.GuildId == GuildId && m.GuildBranchId == GuildBranchId),
            default), Times.Once);
        _auditLog.Verify(a => a.LogAsync(
            GuildId, DiscordId, GuildAuditAction.MemberJoined,
            It.IsAny<Dictionary<string, string>?>(), default), Times.Once);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static GuildBranch MakeBranch(RosterMode? rosterMode, List<string>? rosterRoleIds = null, bool isActive = true)
        => new()
        {
            Id = GuildBranchId,
            GuildId = GuildId,
            BranchId = CharacterBranchId,
            RosterMode = rosterMode,
            RosterRoleIds = rosterRoleIds ?? [],
            IsActive = isActive,
        };

    private void SetupCharacter() =>
        _characters.Setup(r => r.GetByIdAsync(CharacterId, default))
            .ReturnsAsync(new Character { Id = CharacterId, Name = "Arthas", UserDiscordId = DiscordId, ClassId = 5, BranchId = CharacterBranchId });

    private void SetupRegisteredGuild() =>
        _guilds.Setup(r => r.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild { Id = GuildId, Name = "RaidOps", IsRegistered = true });

    private void SetupOpenGuild()
    {
        SetupRegisteredGuild();
        _guildBranches.Setup(b => b.GetByGuildAndBranchAsync(GuildId, CharacterBranchId, default))
            .ReturnsAsync(MakeBranch(rosterMode: RosterMode.Open));
    }

    private void SetupRoleOnlyGuild()
    {
        SetupRegisteredGuild();
        _guildBranches.Setup(b => b.GetByGuildAndBranchAsync(GuildId, CharacterBranchId, default))
            .ReturnsAsync(MakeBranch(rosterMode: RosterMode.DiscordRoleOnly, rosterRoleIds: [RosterRoleId]));
    }

    private void SetupDiscordMember() =>
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId }]);

    private void SetupRoleAccess()
    {
        var guildUser = NetCordTestHelpers.MakeGuildUser(DiscordUlong, 0UL, [RosterRoleUlong]);
        _guild.Setup(gs => gs.GetUsers(GuildId, default)).Returns([guildUser]);
    }
}
