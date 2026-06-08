using FluentAssertions;
using Moq;
using NetCord;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Guilds.Settings.Queries;
using RaidOps.Application.Implementations.Guilds.Settings.QueryHandlers;
using RaidOps.Domain.Models.Discord;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;
using RaidOps.UnitTests.ExternalApplication.Bot;

namespace RaidOps.UnitTests.Application.Guilds.Settings.QueryHandlers;

public class GetGuildDiscordRolesQueryHandlerTests
{
    private readonly Mock<IUserGuildsRepository>           _userGuilds   = new();
    private readonly Mock<IDiscordBotService>              _bot          = new();
    private readonly Mock<IGuildService>                   _guildService = new();
    private readonly GetGuildDiscordRolesQueryHandler      _sut;

    private const string GuildId     = "guild-1";
    private const string RequesterId = "user-1";

    private static readonly GetGuildDiscordRolesQuery Query = new()
    {
        GuildId            = GuildId,
        RequesterDiscordId = RequesterId,
    };

    public GetGuildDiscordRolesQueryHandlerTests()
    {
        _bot.Setup(b => b.Guilds).Returns(_guildService.Object);
        _sut = new GetGuildDiscordRolesQueryHandler(_userGuilds.Object, _bot.Object);
    }

    [Fact]
    public async Task HandleAsync_RequesterNotMember_ReturnsForbidden()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(RequesterId, default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_RequesterNotAdmin_ReturnsForbidden()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(RequesterId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = RequesterId, IsAdmin = false }]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_BotNotPresent_ReturnsBotNotPresent()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(RequesterId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = RequesterId, IsAdmin = true }]);
        _guildService.Setup(g => g.GetRoles(GuildId, default)).Throws<InvalidOperationException>();

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildBotNotPresent);
    }

    [Fact]
    public async Task HandleAsync_Success_ReturnsMappedRoles_NullColor()
    {
        var jsonRole = NetCordTestHelpers.MakeJsonRole(111111111UL, (Permissions)0);
        var netcordGuild = NetCordTestHelpers.MakeGuild(222222222UL, 333333333UL,
            new Dictionary<ulong, GuildUser>(), [jsonRole]);

        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(RequesterId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = RequesterId, IsAdmin = true }]);
        _guildService.Setup(g => g.GetRoles(GuildId, default)).Returns(netcordGuild.Roles.Values);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Id.Should().Be("111111111");
        result.Value[0].Color.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_Success_ReturnsMappedRoles_WithColor()
    {
        var jsonRole = NetCordTestHelpers.MakeJsonRole(222222222UL, (Permissions)0, primaryColor: 0xFF0000);
        var netcordGuild = NetCordTestHelpers.MakeGuild(333333333UL, 444444444UL,
            new Dictionary<ulong, GuildUser>(), [jsonRole]);

        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(RequesterId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = RequesterId, IsAdmin = true }]);
        _guildService.Setup(g => g.GetRoles(GuildId, default)).Returns(netcordGuild.Roles.Values);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result?.Value?[0].Color.Should().Be(0xFF0000);
    }
}
