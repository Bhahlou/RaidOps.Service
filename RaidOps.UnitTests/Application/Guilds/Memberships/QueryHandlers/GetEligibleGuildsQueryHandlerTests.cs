using FluentAssertions;
using Moq;
using NetCord.Gateway;
using RaidOps.Application.Contracts.Common;
using DiscordGuild = RaidOps.Domain.Models.Discord.Guild;
using RaidOps.Application.Contracts.Guilds.Memberships.Queries;
using RaidOps.Application.Implementations.Guilds.Memberships.QueryHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Discord;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;
using RaidOps.UnitTests.ExternalApplication.Bot;

namespace RaidOps.UnitTests.Application.Guilds.Memberships.QueryHandlers;

/// <summary>
/// Unit tests for <see cref="GetEligibleGuildsQueryHandler"/>.
/// </summary>
public class GetEligibleGuildsQueryHandlerTests
{
    private readonly Mock<ICharacterRepository>       _characters    = new();
    private readonly Mock<IGuildsRepository>          _guilds        = new();
    private readonly Mock<IGuildBranchesRepository>   _guildBranches = new();
    private readonly Mock<IUserGuildsRepository>      _userGuilds    = new();
    private readonly Mock<IGuildMembershipRepository> _memberships   = new();
    private readonly Mock<IDiscordBotService>         _bot           = new();
    private readonly Mock<IGuildService>              _guild         = new();
    private readonly GetEligibleGuildsQueryHandler    _sut;

    private const int    CharacterId       = 1;
    private const string GuildId           = "guild-1";
    private const string DiscordId         = "200000000000000001";
    private const ulong  DiscordUlong      = 200000000000000001UL;
    private const string RosterRoleId      = "100000000000000001";
    private const ulong  RosterRoleUlong   = 100000000000000001UL;
    private const int    CharacterBranchId = 10;

    private static readonly GetEligibleGuildsQuery Query = new()
    {
        CharacterId        = CharacterId,
        RequesterDiscordId = DiscordId,
    };

    public GetEligibleGuildsQueryHandlerTests()
    {
        _bot.Setup(b => b.Guilds).Returns(_guild.Object);
        _sut = new GetEligibleGuildsQueryHandler(
            _characters.Object,
            _guilds.Object,
            _guildBranches.Object,
            _userGuilds.Object,
            _memberships.Object,
            _bot.Object);
    }

    private static GuildBranch MakeBranch(RosterMode? rosterMode, List<string>? rosterRoleIds = null, bool isActive = true)
        => new()
        {
            Id = 1, GuildId = GuildId, BranchId = CharacterBranchId,
            RosterMode = rosterMode, RosterRoleIds = rosterRoleIds ?? [], IsActive = isActive,
        };

    // ── CharacterNotFound ─────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_CharacterNotFound_ReturnsCharacterNotFound()
    {
        _characters.Setup(r => r.GetByIdAsync(CharacterId, default)).ReturnsAsync((Character?)null);

        var result = await _sut.HandleAsync(Query, new CancellationToken());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.CharacterNotFound);
    }

    // ── CharacterNotOwned ─────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_CharacterNotOwned_ReturnsCharacterNotOwned()
    {
        _characters.Setup(r => r.GetByIdAsync(CharacterId, default))
            .ReturnsAsync(new Character { Id = CharacterId, Name = "Arthas", UserDiscordId = "other-user" });

        var result = await _sut.HandleAsync(Query, new CancellationToken());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.CharacterNotOwned);
    }

    // ── NoDiscordGuilds ───────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_NoDiscordGuilds_ReturnsEmptyList()
    {
        SetupCharacter();
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default)).ReturnsAsync([]);
        _memberships.Setup(r => r.GetByCharacterIdAsync(CharacterId, default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(Query, new CancellationToken());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // ── AlreadyMember — Excluded ──────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_AlreadyMember_GuildExcluded()
    {
        SetupCharacter();
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId }]);
        _memberships.Setup(r => r.GetByCharacterIdAsync(CharacterId, default))
            .ReturnsAsync([new GuildMembership { CharacterId = CharacterId, GuildId = GuildId }]);

        var result = await _sut.HandleAsync(Query, new CancellationToken());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // ── GuildNotFound — Excluded ──────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_GuildNotFound_GuildExcluded()
    {
        SetupCharacter();
        SetupUserInGuild();
        SetupNoExistingMembership();
        _guilds.Setup(r => r.GetByIdAsync(GuildId, default)).ReturnsAsync((DiscordGuild?)null);

        var result = await _sut.HandleAsync(Query, new CancellationToken());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // ── GuildNotRegistered — Excluded ─────────────────────────────────────

    [Fact]
    public async Task HandleAsync_GuildNotRegistered_GuildExcluded()
    {
        SetupCharacter();
        SetupUserInGuild();
        SetupNoExistingMembership();
        _guilds.Setup(r => r.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild { Id = GuildId, Name = "RaidOps", IsRegistered = false });

        var result = await _sut.HandleAsync(Query, new CancellationToken());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // ── Character's branch not active on guild — Excluded ─────────────────

    [Fact]
    public async Task HandleAsync_CharacterBranchNotActiveOnGuild_GuildExcluded()
    {
        SetupCharacter();
        SetupUserInGuild();
        SetupNoExistingMembership();
        _guilds.Setup(r => r.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild { Id = GuildId, Name = "RaidOps", IsRegistered = true });
        _guildBranches.Setup(b => b.GetByGuildAndBranchAsync(GuildId, CharacterBranchId, default))
            .ReturnsAsync((GuildBranch?)null);

        var result = await _sut.HandleAsync(Query, new CancellationToken());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_CharacterBranchDeactivated_GuildExcluded()
    {
        SetupCharacter();
        SetupUserInGuild();
        SetupNoExistingMembership();
        _guilds.Setup(r => r.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild { Id = GuildId, Name = "RaidOps", IsRegistered = true });
        _guildBranches.Setup(b => b.GetByGuildAndBranchAsync(GuildId, CharacterBranchId, default))
            .ReturnsAsync(MakeBranch(rosterMode: RosterMode.Open, isActive: false));

        var result = await _sut.HandleAsync(Query, new CancellationToken());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // ── RosterMode null — Excluded ────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_RosterModeNull_GuildExcluded()
    {
        SetupCharacter();
        SetupUserInGuild();
        SetupNoExistingMembership();
        _guilds.Setup(r => r.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild { Id = GuildId, Name = "RaidOps", IsRegistered = true });
        _guildBranches.Setup(b => b.GetByGuildAndBranchAsync(GuildId, CharacterBranchId, default))
            .ReturnsAsync(MakeBranch(rosterMode: null));

        var result = await _sut.HandleAsync(Query, new CancellationToken());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // ── Open — Included ───────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_OpenGuild_GuildIncluded()
    {
        SetupCharacter();
        SetupUserInGuild();
        SetupNoExistingMembership();
        _guilds.Setup(r => r.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild { Id = GuildId, Name = "RaidOps", IsRegistered = true });
        _guildBranches.Setup(b => b.GetByGuildAndBranchAsync(GuildId, CharacterBranchId, default))
            .ReturnsAsync(MakeBranch(rosterMode: RosterMode.Open));

        var result = await _sut.HandleAsync(Query, new CancellationToken());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(g => g.GuildId == GuildId && g.GuildName == "RaidOps");
    }

    // ── DiscordRoleOnly with empty RosterRoleIds — Excluded, bot never queried ──

    [Fact]
    public async Task HandleAsync_DiscordRoleOnly_RosterRoleIdsEmpty_GuildExcludedWithoutQueryingBot()
    {
        SetupCharacter();
        SetupUserInGuild();
        SetupNoExistingMembership();
        _guilds.Setup(r => r.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild { Id = GuildId, Name = "RaidOps", IsRegistered = true });
        _guildBranches.Setup(b => b.GetByGuildAndBranchAsync(GuildId, CharacterBranchId, default))
            .ReturnsAsync(MakeBranch(rosterMode: RosterMode.DiscordRoleOnly, rosterRoleIds: []));

        var result = await _sut.HandleAsync(Query, new CancellationToken());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
        _guild.Verify(gs => gs.GetUsers(It.IsAny<string>(), default), Times.Never);
    }

    // ── DiscordRoleOnly — BotNotPresent — Excluded silently ───────────────

    [Fact]
    public async Task HandleAsync_DiscordRoleOnly_BotNotPresent_GuildExcluded()
    {
        SetupCharacter();
        SetupUserInGuild();
        SetupNoExistingMembership();
        SetupRoleOnlyGuild();
        _guild.Setup(gs => gs.GetUsers(GuildId, default)).Throws<InvalidOperationException>();

        var result = await _sut.HandleAsync(Query, new CancellationToken());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // ── DiscordRoleOnly — User not in Discord guild — Excluded ───────────

    [Fact]
    public async Task HandleAsync_DiscordRoleOnly_UserNotInDiscordGuild_GuildExcluded()
    {
        SetupCharacter();
        SetupUserInGuild();
        SetupNoExistingMembership();
        SetupRoleOnlyGuild();
        _guild.Setup(gs => gs.GetUsers(GuildId, default)).Returns([]);

        var result = await _sut.HandleAsync(Query, new CancellationToken());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // ── DiscordRoleOnly — User lacks any roster role — Excluded ───────────

    [Fact]
    public async Task HandleAsync_DiscordRoleOnly_UserLacksRole_GuildExcluded()
    {
        SetupCharacter();
        SetupUserInGuild();
        SetupNoExistingMembership();
        SetupRoleOnlyGuild();

        const ulong unrelatedRoleUlong = 999999999999999999UL;
        var guildUser = NetCordTestHelpers.MakeGuildUser(DiscordUlong, 0UL, [unrelatedRoleUlong]);
        _guild.Setup(gs => gs.GetUsers(GuildId, default)).Returns([guildUser]);

        var result = await _sut.HandleAsync(Query, new CancellationToken());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // ── DiscordRoleOnly — User has role — Included ────────────────────────

    [Fact]
    public async Task HandleAsync_DiscordRoleOnly_UserHasRole_GuildIncluded()
    {
        SetupCharacter();
        SetupUserInGuild();
        SetupNoExistingMembership();
        SetupRoleOnlyGuild();
        SetupRoleAccess();

        var result = await _sut.HandleAsync(Query, new CancellationToken());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(g => g.GuildId == GuildId && g.GuildName == "RaidOps");
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void SetupCharacter() =>
        _characters.Setup(r => r.GetByIdAsync(CharacterId, default))
            .ReturnsAsync(new Character { Id = CharacterId, Name = "Arthas", UserDiscordId = DiscordId, BranchId = CharacterBranchId });

    private void SetupUserInGuild() =>
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId }]);

    private void SetupNoExistingMembership() =>
        _memberships.Setup(r => r.GetByCharacterIdAsync(CharacterId, default)).ReturnsAsync([]);

    private void SetupRoleOnlyGuild()
    {
        _guilds.Setup(r => r.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild { Id = GuildId, Name = "RaidOps", IsRegistered = true });
        _guildBranches.Setup(b => b.GetByGuildAndBranchAsync(GuildId, CharacterBranchId, default))
            .ReturnsAsync(MakeBranch(rosterMode: RosterMode.DiscordRoleOnly, rosterRoleIds: [RosterRoleId]));
    }

    private void SetupRoleAccess()
    {
        var guildUser = NetCordTestHelpers.MakeGuildUser(DiscordUlong, 0UL, [RosterRoleUlong]);
        _guild.Setup(gs => gs.GetUsers(GuildId, default)).Returns([guildUser]);
    }
}
