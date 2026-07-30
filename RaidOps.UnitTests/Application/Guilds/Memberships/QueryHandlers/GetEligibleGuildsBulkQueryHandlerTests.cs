using FluentAssertions;
using Moq;
using NetCord.Gateway;
using DiscordGuild = RaidOps.Domain.Models.Discord.Guild;
using RaidOps.Application.Contracts.Guilds.Memberships.Queries;
using RaidOps.Application.Implementations.Guilds.Memberships.QueryHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Reference;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;
using RaidOps.UnitTests.ExternalApplication.Bot;

namespace RaidOps.UnitTests.Application.Guilds.Memberships.QueryHandlers;

/// <summary>
/// Unit tests for <see cref="GetEligibleGuildsBulkQueryHandler"/>.
/// </summary>
public class GetEligibleGuildsBulkQueryHandlerTests
{
    private readonly Mock<ICharacterRepository>       _characters    = new();
    private readonly Mock<IGuildsRepository>          _guilds        = new();
    private readonly Mock<IGuildBranchesRepository>   _guildBranches = new();
    private readonly Mock<IUserGuildsRepository>      _userGuilds    = new();
    private readonly Mock<IGuildMembershipRepository> _memberships   = new();
    private readonly Mock<IDiscordBotService>         _bot           = new();
    private readonly Mock<IGuildService>              _guild         = new();
    private readonly GetEligibleGuildsBulkQueryHandler _sut;

    private const string DiscordId    = "200000000000000001";
    private const ulong  DiscordUlong = 200000000000000001UL;
    private const string GuildId      = "guild-1";
    private const int    Char1Id      = 1;
    private const int    Char2Id      = 2;
    private const int    BranchId     = 10;
    private const string RosterRoleId = "100000000000000001";
    private const ulong  RosterRoleUlong = 100000000000000001UL;

    private static readonly GetEligibleGuildsBulkQuery Query = new() { RequesterDiscordId = DiscordId };

    private static readonly WowClass TestClass = new() { Id = 8, Name = "Mage", Color = "69CCF0" };

    private static Character MakeChar(int id, int branchId = BranchId) => new()
    {
        Id = id, Name = $"Char{id}", UserDiscordId = DiscordId, BranchId = branchId,
        ClassId = TestClass.Id, Class = TestClass,
    };

    private static GuildBranch MakeBranch(RosterMode? rosterMode, List<string>? rosterRoleIds = null, int branchId = BranchId)
        => new() { Id = 1, GuildId = GuildId, BranchId = branchId, RosterMode = rosterMode, RosterRoleIds = rosterRoleIds ?? [], IsActive = true };

    public GetEligibleGuildsBulkQueryHandlerTests()
    {
        _bot.Setup(b => b.Guilds).Returns(_guild.Object);
        _sut = new GetEligibleGuildsBulkQueryHandler(
            _characters.Object,
            _guilds.Object,
            _guildBranches.Object,
            _userGuilds.Object,
            _memberships.Object,
            _bot.Object);
    }

    // ── No active characters ──────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_NoActiveCharacters_ReturnsEmptyList()
    {
        _characters.Setup(r => r.GetByUserWithDetailsAsync(DiscordId, true, default))
            .ReturnsAsync([]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
        _memberships.Verify(r => r.GetByCharacterIdsAsync(It.IsAny<IEnumerable<int>>(), default), Times.Never);
    }

    // ── No Discord guilds ─────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_NoDiscordGuilds_ReturnsEmptyList()
    {
        SetupCharacters(MakeChar(Char1Id));
        SetupNoMemberships();
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // ── Open guild — included with all eligible chars ─────────────────────

    [Fact]
    public async Task HandleAsync_OpenGuild_ReturnsGuildWithEligibleCharacters()
    {
        SetupCharacters(MakeChar(Char1Id), MakeChar(Char2Id));
        SetupNoMemberships();
        SetupUserInGuild();
        SetupRegisteredGuild();
        _guildBranches.Setup(b => b.GetActiveForGuildAsync(GuildId, default))
            .ReturnsAsync([MakeBranch(rosterMode: RosterMode.Open)]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(g => g.GuildId == GuildId);
        result.Value![0].EligibleCharacters.Should().HaveCount(2);
        result.Value![0].EligibleCharacters.Should().Contain(c => c.Id == Char1Id);
        result.Value![0].EligibleCharacters.Should().Contain(c => c.Id == Char2Id);
    }

    [Fact]
    public async Task HandleAsync_OpenGuild_EligibleCharacterDtoHasCorrectClassFields()
    {
        SetupCharacters(MakeChar(Char1Id));
        SetupNoMemberships();
        SetupUserInGuild();
        SetupRegisteredGuild();
        _guildBranches.Setup(b => b.GetActiveForGuildAsync(GuildId, default))
            .ReturnsAsync([MakeBranch(rosterMode: RosterMode.Open)]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        var charDto = result.Value![0].EligibleCharacters[0];
        charDto.ClassId.Should().Be(TestClass.Id);
        charDto.ClassName.Should().Be(TestClass.Name);
        charDto.ClassColor.Should().Be($"#{TestClass.Color}");
    }

    // ── Already member — excluded per character ───────────────────────────

    [Fact]
    public async Task HandleAsync_OneCharAlreadyMember_ExcludesOnlyThatChar()
    {
        SetupCharacters(MakeChar(Char1Id), MakeChar(Char2Id));
        _memberships.Setup(r => r.GetByCharacterIdsAsync(It.IsAny<IEnumerable<int>>(), default))
            .ReturnsAsync([new GuildMembership { CharacterId = Char1Id, GuildId = GuildId }]);
        SetupUserInGuild();
        SetupRegisteredGuild();
        _guildBranches.Setup(b => b.GetActiveForGuildAsync(GuildId, default))
            .ReturnsAsync([MakeBranch(rosterMode: RosterMode.Open)]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value![0].EligibleCharacters.Should().ContainSingle(c => c.Id == Char2Id);
    }

    [Fact]
    public async Task HandleAsync_AllCharsAlreadyMembers_GuildExcluded()
    {
        SetupCharacters(MakeChar(Char1Id));
        _memberships.Setup(r => r.GetByCharacterIdsAsync(It.IsAny<IEnumerable<int>>(), default))
            .ReturnsAsync([new GuildMembership { CharacterId = Char1Id, GuildId = GuildId }]);
        SetupUserInGuild();
        SetupRegisteredGuild();
        _guildBranches.Setup(b => b.GetActiveForGuildAsync(GuildId, default))
            .ReturnsAsync([MakeBranch(rosterMode: RosterMode.Open)]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // ── Exclusion filters (registration / roster mode / branch) ───────────

    [Fact]
    public async Task HandleAsync_GuildNotFound_GuildExcluded()
    {
        SetupCharacters(MakeChar(Char1Id));
        SetupNoMemberships();
        SetupUserInGuild();
        _guilds.Setup(r => r.GetByIdAsync(GuildId, default)).ReturnsAsync((DiscordGuild?)null);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_GuildNotRegistered_GuildExcluded()
    {
        SetupCharacters(MakeChar(Char1Id));
        SetupNoMemberships();
        SetupUserInGuild();
        _guilds.Setup(r => r.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild { Id = GuildId, Name = "Iron Council", IsRegistered = false });

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_NoActiveBranches_GuildExcluded()
    {
        SetupCharacters(MakeChar(Char1Id));
        SetupNoMemberships();
        SetupUserInGuild();
        SetupRegisteredGuild();
        _guildBranches.Setup(b => b.GetActiveForGuildAsync(GuildId, default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_CharacterBranchNotAmongActiveBranches_CharacterExcluded()
    {
        SetupCharacters(MakeChar(Char1Id, branchId: 999));
        SetupNoMemberships();
        SetupUserInGuild();
        SetupRegisteredGuild();
        _guildBranches.Setup(b => b.GetActiveForGuildAsync(GuildId, default))
            .ReturnsAsync([MakeBranch(rosterMode: RosterMode.Open, branchId: BranchId)]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_RosterModeNull_GuildExcluded()
    {
        SetupCharacters(MakeChar(Char1Id));
        SetupNoMemberships();
        SetupUserInGuild();
        SetupRegisteredGuild();
        _guildBranches.Setup(b => b.GetActiveForGuildAsync(GuildId, default))
            .ReturnsAsync([MakeBranch(rosterMode: null)]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // ── DiscordRoleOnly — RosterRoleIds empty — excluded, bot never queried ──

    [Fact]
    public async Task HandleAsync_DiscordRoleOnly_RosterRoleIdsEmpty_GuildExcludedWithoutQueryingBot()
    {
        SetupCharacters(MakeChar(Char1Id));
        SetupNoMemberships();
        SetupUserInGuild();
        SetupRegisteredGuild();
        _guildBranches.Setup(b => b.GetActiveForGuildAsync(GuildId, default))
            .ReturnsAsync([MakeBranch(rosterMode: RosterMode.DiscordRoleOnly, rosterRoleIds: [])]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
        _guild.Verify(gs => gs.GetUsers(It.IsAny<string>(), default), Times.Never);
    }

    // ── DiscordRoleOnly — bot not present — excluded silently ─────────────

    [Fact]
    public async Task HandleAsync_DiscordRoleOnly_BotNotPresent_GuildExcluded()
    {
        SetupCharacters(MakeChar(Char1Id));
        SetupNoMemberships();
        SetupUserInGuild();
        SetupRoleOnlyGuild();
        _guild.Setup(gs => gs.GetUsers(GuildId, default)).Throws<InvalidOperationException>();

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // ── DiscordRoleOnly — user not in Discord guild — excluded ────────────

    [Fact]
    public async Task HandleAsync_DiscordRoleOnly_UserNotInDiscordGuild_GuildExcluded()
    {
        SetupCharacters(MakeChar(Char1Id));
        SetupNoMemberships();
        SetupUserInGuild();
        SetupRoleOnlyGuild();
        _guild.Setup(gs => gs.GetUsers(GuildId, default)).Returns([]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // ── DiscordRoleOnly — user lacks role — excluded ──────────────────────

    [Fact]
    public async Task HandleAsync_DiscordRoleOnly_UserLacksRole_GuildExcluded()
    {
        const ulong unrelatedRoleUlong = 999999999999999999UL;

        SetupCharacters(MakeChar(Char1Id));
        SetupNoMemberships();
        SetupUserInGuild();
        SetupRoleOnlyGuild();
        var guildUser = NetCordTestHelpers.MakeGuildUser(DiscordUlong, 0UL, [unrelatedRoleUlong]);
        _guild.Setup(gs => gs.GetUsers(GuildId, default)).Returns([guildUser]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // ── DiscordRoleOnly — user has role — included ────────────────────────

    [Fact]
    public async Task HandleAsync_DiscordRoleOnly_UserHasRole_GuildIncluded()
    {
        SetupCharacters(MakeChar(Char1Id));
        SetupNoMemberships();
        SetupUserInGuild();
        SetupRoleOnlyGuild();
        var guildUser = NetCordTestHelpers.MakeGuildUser(DiscordUlong, 0UL, [RosterRoleUlong]);
        _guild.Setup(gs => gs.GetUsers(GuildId, default)).Returns([guildUser]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(g => g.GuildId == GuildId);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void SetupCharacters(params Character[] chars) =>
        _characters.Setup(r => r.GetByUserWithDetailsAsync(DiscordId, true, default))
            .ReturnsAsync(chars);

    private void SetupNoMemberships() =>
        _memberships.Setup(r => r.GetByCharacterIdsAsync(It.IsAny<IEnumerable<int>>(), default))
            .ReturnsAsync([]);

    private void SetupUserInGuild() =>
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId }]);

    private void SetupRegisteredGuild() =>
        _guilds.Setup(r => r.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild { Id = GuildId, Name = "Iron Council", IsRegistered = true });

    private void SetupRoleOnlyGuild()
    {
        SetupRegisteredGuild();
        _guildBranches.Setup(b => b.GetActiveForGuildAsync(GuildId, default))
            .ReturnsAsync([MakeBranch(rosterMode: RosterMode.DiscordRoleOnly, rosterRoleIds: [RosterRoleId])]);
    }
}
