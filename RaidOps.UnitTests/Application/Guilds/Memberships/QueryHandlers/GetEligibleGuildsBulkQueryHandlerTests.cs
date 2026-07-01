using FluentAssertions;
using Moq;
using NetCord;
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
    private readonly Mock<ICharacterRepository>       _characters  = new();
    private readonly Mock<IGuildsRepository>          _guilds      = new();
    private readonly Mock<IUserGuildsRepository>      _userGuilds  = new();
    private readonly Mock<IGuildMembershipRepository> _memberships = new();
    private readonly Mock<IDiscordBotService>         _bot         = new();
    private readonly Mock<IGuildService>              _guild       = new();
    private readonly GetEligibleGuildsBulkQueryHandler _sut;

    private const string DiscordId    = "200000000000000001";
    private const string GuildId      = "guild-1";
    private const int    Char1Id      = 1;
    private const int    Char2Id      = 2;

    private static readonly GetEligibleGuildsBulkQuery Query = new() { RequesterDiscordId = DiscordId };

    private static readonly WowClass TestClass = new() { Id = 8, Name = "Mage", Color = "69CCF0" };

    private static Character MakeChar(int id) => new()
    {
        Id = id, Name = $"Char{id}", UserDiscordId = DiscordId,
        ClassId = TestClass.Id, Class = TestClass,
    };

    public GetEligibleGuildsBulkQueryHandlerTests()
    {
        _bot.Setup(b => b.Guilds).Returns(_guild.Object);
        _sut = new GetEligibleGuildsBulkQueryHandler(
            _characters.Object,
            _guilds.Object,
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
        _guilds.Setup(r => r.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild { Id = GuildId, Name = "Iron Council", IsRegistered = true, RosterMode = RosterMode.Open });

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(g => g.GuildId == GuildId);
        result.Value[0].EligibleCharacters.Should().HaveCount(2);
        result.Value[0].EligibleCharacters.Should().Contain(c => c.Id == Char1Id);
        result.Value[0].EligibleCharacters.Should().Contain(c => c.Id == Char2Id);
    }

    [Fact]
    public async Task HandleAsync_OpenGuild_EligibleCharacterDtoHasCorrectClassFields()
    {
        SetupCharacters(MakeChar(Char1Id));
        SetupNoMemberships();
        SetupUserInGuild();
        _guilds.Setup(r => r.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild { Id = GuildId, Name = "Iron Council", IsRegistered = true, RosterMode = RosterMode.Open });

        var result = await _sut.HandleAsync(Query, default);

        var charDto = result.Value[0].EligibleCharacters[0];
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
        _guilds.Setup(r => r.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild { Id = GuildId, Name = "Iron Council", IsRegistered = true, RosterMode = RosterMode.Open });

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].EligibleCharacters.Should().ContainSingle(c => c.Id == Char2Id);
    }

    [Fact]
    public async Task HandleAsync_AllCharsAlreadyMembers_GuildExcluded()
    {
        SetupCharacters(MakeChar(Char1Id));
        _memberships.Setup(r => r.GetByCharacterIdsAsync(It.IsAny<IEnumerable<int>>(), default))
            .ReturnsAsync([new GuildMembership { CharacterId = Char1Id, GuildId = GuildId }]);
        SetupUserInGuild();
        _guilds.Setup(r => r.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild { Id = GuildId, Name = "Iron Council", IsRegistered = true, RosterMode = RosterMode.Open });

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // ── Exclusion filters (registration / roster mode) ────────────────────

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
    public async Task HandleAsync_RosterModeNull_GuildExcluded()
    {
        SetupCharacters(MakeChar(Char1Id));
        SetupNoMemberships();
        SetupUserInGuild();
        _guilds.Setup(r => r.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild { Id = GuildId, Name = "Iron Council", IsRegistered = true, RosterMode = null });

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // ── DiscordRoleOnly — MinRosterRoleId null — excluded, bot never queried ──

    [Fact]
    public async Task HandleAsync_DiscordRoleOnly_MinRosterRoleIdNull_GuildExcludedWithoutQueryingBot()
    {
        SetupCharacters(MakeChar(Char1Id));
        SetupNoMemberships();
        SetupUserInGuild();
        _guilds.Setup(r => r.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild
            {
                Id = GuildId, Name = "Iron Council", IsRegistered = true,
                RosterMode = RosterMode.DiscordRoleOnly, MinRosterRoleId = null,
            });

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
        _guild.Verify(gs => gs.GetRoles(It.IsAny<string>(), default), Times.Never);
    }

    // ── DiscordRoleOnly — bot not present — excluded silently ─────────────

    [Fact]
    public async Task HandleAsync_DiscordRoleOnly_BotNotPresent_GuildExcluded()
    {
        SetupCharacters(MakeChar(Char1Id));
        SetupNoMemberships();
        SetupUserInGuild();
        SetupRoleOnlyGuild();
        _guild.Setup(gs => gs.GetRoles(GuildId, default)).Throws<InvalidOperationException>();

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // ── DiscordRoleOnly — min role not found in Discord — excluded ────────

    [Fact]
    public async Task HandleAsync_DiscordRoleOnly_MinRoleNotFound_GuildExcluded()
    {
        const ulong otherRoleUlong = 999UL;

        SetupCharacters(MakeChar(Char1Id));
        SetupNoMemberships();
        SetupUserInGuild();
        SetupRoleOnlyGuild();

        var otherRole = NetCordTestHelpers.MakeJsonRole(otherRoleUlong, (Permissions)0, position: 5);
        var g = NetCordTestHelpers.MakeGuild(0UL, 0UL, new Dictionary<ulong, GuildUser>(), [otherRole]);
        _guild.Setup(gs => gs.GetRoles(GuildId, default)).Returns(g.Roles.Values);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // ── DiscordRoleOnly — user not in Discord guild — excluded ────────────

    [Fact]
    public async Task HandleAsync_DiscordRoleOnly_UserNotInDiscordGuild_GuildExcluded()
    {
        const ulong minRoleUlong = 100000000000000001UL;

        SetupCharacters(MakeChar(Char1Id));
        SetupNoMemberships();
        SetupUserInGuild();
        SetupRoleOnlyGuild();
        SetupMinRole(minRoleUlong, position: 5);
        _guild.Setup(gs => gs.GetUsers(GuildId, default)).Returns([]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // ── DiscordRoleOnly — user lacks role — excluded ──────────────────────

    [Fact]
    public async Task HandleAsync_DiscordRoleOnly_UserLacksRole_GuildExcluded()
    {
        const ulong  minRoleUlong  = 100000000000000001UL;
        const ulong  lowRoleUlong  = 100000000000000002UL;
        const ulong  discordUlong  = 200000000000000001UL;

        SetupCharacters(MakeChar(Char1Id));
        SetupNoMemberships();
        SetupUserInGuild();
        SetupRoleOnlyGuild();

        var minJson = NetCordTestHelpers.MakeJsonRole(minRoleUlong, (Permissions)0, position: 10);
        var lowJson = NetCordTestHelpers.MakeJsonRole(lowRoleUlong, (Permissions)0, position: 3);
        var g = NetCordTestHelpers.MakeGuild(0UL, 0UL, new Dictionary<ulong, GuildUser>(), [minJson, lowJson]);
        _guild.Setup(gs => gs.GetRoles(GuildId, default)).Returns(g.Roles.Values);

        var guildUser = NetCordTestHelpers.MakeGuildUser(discordUlong, 0UL, [lowRoleUlong]);
        _guild.Setup(gs => gs.GetUsers(GuildId, default)).Returns([guildUser]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // ── DiscordRoleOnly — user has unknown role ID — excluded ─────────────

    [Fact]
    public async Task HandleAsync_DiscordRoleOnly_UserHasUnknownRoleId_GuildExcluded()
    {
        const ulong minRoleUlong     = 100000000000000001UL;
        const ulong unknownRoleUlong = 999999999999999999UL;
        const ulong discordUlong     = 200000000000000001UL;

        SetupCharacters(MakeChar(Char1Id));
        SetupNoMemberships();
        SetupUserInGuild();
        SetupRoleOnlyGuild();

        var minJson = NetCordTestHelpers.MakeJsonRole(minRoleUlong, (Permissions)0, position: 5);
        var g = NetCordTestHelpers.MakeGuild(0UL, 0UL, new Dictionary<ulong, GuildUser>(), [minJson]);
        _guild.Setup(gs => gs.GetRoles(GuildId, default)).Returns(g.Roles.Values);

        var guildUser = NetCordTestHelpers.MakeGuildUser(discordUlong, 0UL, [unknownRoleUlong]);
        _guild.Setup(gs => gs.GetUsers(GuildId, default)).Returns([guildUser]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // ── DiscordRoleOnly — user has role — included ────────────────────────

    [Fact]
    public async Task HandleAsync_DiscordRoleOnly_UserHasRole_GuildIncluded()
    {
        const string minRoleId    = "100000000000000001";
        const ulong  minRoleUlong = 100000000000000001UL;
        const ulong  discordUlong = 200000000000000001UL;

        SetupCharacters(MakeChar(Char1Id));
        SetupNoMemberships();
        SetupUserInGuild();
        _guilds.Setup(r => r.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild
            {
                Id = GuildId, Name = "Iron Council", IsRegistered = true,
                RosterMode = RosterMode.DiscordRoleOnly, MinRosterRoleId = minRoleId,
            });

        var jsonRole = NetCordTestHelpers.MakeJsonRole(minRoleUlong, (Permissions)0, position: 5);
        var g = NetCordTestHelpers.MakeGuild(0UL, 0UL, new Dictionary<ulong, GuildUser>(), [jsonRole]);
        _guild.Setup(gs => gs.GetRoles(GuildId, default)).Returns(g.Roles.Values);

        var guildUser = NetCordTestHelpers.MakeGuildUser(discordUlong, 0UL, [minRoleUlong]);
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

    private void SetupRoleOnlyGuild() =>
        _guilds.Setup(r => r.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild
            {
                Id = GuildId, Name = "Iron Council", IsRegistered = true,
                RosterMode = RosterMode.DiscordRoleOnly, MinRosterRoleId = "100000000000000001",
            });

    private void SetupMinRole(ulong roleId, int position)
    {
        var jsonRole = NetCordTestHelpers.MakeJsonRole(roleId, (Permissions)0, position: position);
        var g = NetCordTestHelpers.MakeGuild(0UL, 0UL, new Dictionary<ulong, GuildUser>(), [jsonRole]);
        _guild.Setup(gs => gs.GetRoles(GuildId, default)).Returns(g.Roles.Values);
    }
}
