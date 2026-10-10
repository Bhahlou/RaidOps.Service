using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NetCord.Gateway;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Implementations.Guilds.Access;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;
using RaidOps.UnitTests.ExternalApplication.Bot;

namespace RaidOps.UnitTests.Application.Guilds.Access;

/// <summary>
/// Unit tests for <see cref="GuildJoinEligibilityService"/>.
/// </summary>
public class GuildJoinEligibilityServiceTests
{
    private readonly Mock<IGuildBranchesRepository> _guildBranches = new();
    private readonly Mock<IDiscordBotService>       _bot           = new();
    private readonly Mock<IGuildService>            _guild         = new();
    private readonly GuildJoinEligibilityService    _sut;

    private const string GuildId          = "guild-1";
    private const string DiscordId        = "200000000000000001";
    private const ulong  DiscordUlong     = 200000000000000001UL;
    private const string RosterRoleId     = "100000000000000001";
    private const ulong  RosterRoleUlong  = 100000000000000001UL;
    private const int    CharacterBranchId = 10;
    private const int    GuildBranchId    = 1;

    public GuildJoinEligibilityServiceTests()
    {
        _bot.Setup(b => b.Guilds).Returns(_guild.Object);
        _sut = new GuildJoinEligibilityService(_guildBranches.Object, _bot.Object, NullLogger<GuildJoinEligibilityService>.Instance);
    }

    // ── Branch not active on this guild ───────────────────────────────────

    [Fact]
    public async Task ResolveEligibleBranchAsync_BranchNotFound_ReturnsGuildBranchNotActive()
    {
        _guildBranches.Setup(b => b.GetByGuildAndBranchAsync(GuildId, CharacterBranchId, default))
            .ReturnsAsync((GuildBranch?)null);

        var result = await _sut.ResolveEligibleBranchAsync(GuildId, CharacterBranchId, DiscordId, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildBranchNotActive);
    }

    [Fact]
    public async Task ResolveEligibleBranchAsync_BranchDeactivated_ReturnsGuildBranchNotActive()
    {
        _guildBranches.Setup(b => b.GetByGuildAndBranchAsync(GuildId, CharacterBranchId, default))
            .ReturnsAsync(MakeBranch(rosterMode: RosterMode.Open, isActive: false));

        var result = await _sut.ResolveEligibleBranchAsync(GuildId, CharacterBranchId, DiscordId, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildBranchNotActive);
    }

    // ── GuildNotConfigured (branch RosterMode null) ───────────────────────

    [Fact]
    public async Task ResolveEligibleBranchAsync_RosterModeNull_ReturnsGuildNotConfigured()
    {
        _guildBranches.Setup(b => b.GetByGuildAndBranchAsync(GuildId, CharacterBranchId, default))
            .ReturnsAsync(MakeBranch(rosterMode: null));

        var result = await _sut.ResolveEligibleBranchAsync(GuildId, CharacterBranchId, DiscordId, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildNotConfigured);
    }

    // ── Open — Success ────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveEligibleBranchAsync_Open_ReturnsTheBranch()
    {
        var branch = MakeBranch(rosterMode: RosterMode.Open);
        _guildBranches.Setup(b => b.GetByGuildAndBranchAsync(GuildId, CharacterBranchId, default))
            .ReturnsAsync(branch);

        var result = await _sut.ResolveEligibleBranchAsync(GuildId, CharacterBranchId, DiscordId, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(branch);
        _bot.Verify(b => b.Guilds, Times.Never); // Open mode never needs a live Discord lookup.
    }

    // ── DiscordRoleOnly — RosterRoleIds empty ─────────────────────────────

    [Fact]
    public async Task ResolveEligibleBranchAsync_DiscordRoleOnly_RosterRoleIdsEmpty_ReturnsGuildNotConfigured()
    {
        _guildBranches.Setup(b => b.GetByGuildAndBranchAsync(GuildId, CharacterBranchId, default))
            .ReturnsAsync(MakeBranch(rosterMode: RosterMode.DiscordRoleOnly, rosterRoleIds: []));

        var result = await _sut.ResolveEligibleBranchAsync(GuildId, CharacterBranchId, DiscordId, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildNotConfigured);
    }

    // ── DiscordRoleOnly — BotNotPresent ───────────────────────────────────

    [Fact]
    public async Task ResolveEligibleBranchAsync_DiscordRoleOnly_BotNotPresent_ReturnsBotNotPresent()
    {
        SetupRoleOnlyBranch();
        _guild.Setup(g => g.GetUsers(GuildId, default)).Throws<InvalidOperationException>();

        var result = await _sut.ResolveEligibleBranchAsync(GuildId, CharacterBranchId, DiscordId, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildBotNotPresent);
    }

    // ── DiscordRoleOnly — User not found in the Discord guild ─────────────

    [Fact]
    public async Task ResolveEligibleBranchAsync_DiscordRoleOnly_UserNotInDiscordGuild_ReturnsRosterAccessDenied()
    {
        SetupRoleOnlyBranch();
        _guild.Setup(g => g.GetUsers(GuildId, default)).Returns([]);

        var result = await _sut.ResolveEligibleBranchAsync(GuildId, CharacterBranchId, DiscordId, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RosterAccessDenied);
    }

    // ── DiscordRoleOnly — User has none of the roster roles ────────────────

    [Fact]
    public async Task ResolveEligibleBranchAsync_DiscordRoleOnly_UserLacksRole_ReturnsRosterAccessDenied()
    {
        SetupRoleOnlyBranch();

        const ulong unrelatedRoleUlong = 999999999999999999UL;
        var guildUser = NetCordTestHelpers.MakeGuildUser(DiscordUlong, 0UL, [unrelatedRoleUlong]);
        _guild.Setup(g => g.GetUsers(GuildId, default)).Returns([guildUser]);

        var result = await _sut.ResolveEligibleBranchAsync(GuildId, CharacterBranchId, DiscordId, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RosterAccessDenied);
    }

    // ── DiscordRoleOnly — Success ─────────────────────────────────────────

    [Fact]
    public async Task ResolveEligibleBranchAsync_DiscordRoleOnly_UserHasRole_ReturnsTheBranch()
    {
        var branch = SetupRoleOnlyBranch();
        var guildUser = NetCordTestHelpers.MakeGuildUser(DiscordUlong, 0UL, [RosterRoleUlong]);
        _guild.Setup(g => g.GetUsers(GuildId, default)).Returns([guildUser]);

        var result = await _sut.ResolveEligibleBranchAsync(GuildId, CharacterBranchId, DiscordId, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(branch);
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

    private GuildBranch SetupRoleOnlyBranch()
    {
        var branch = MakeBranch(rosterMode: RosterMode.DiscordRoleOnly, rosterRoleIds: [RosterRoleId]);
        _guildBranches.Setup(b => b.GetByGuildAndBranchAsync(GuildId, CharacterBranchId, default))
            .ReturnsAsync(branch);
        return branch;
    }
}
