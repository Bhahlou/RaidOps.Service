using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Guilds.Registration.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Guilds.Registration.CommandHandlers;
using RaidOps.Domain.Models.Discord;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Guilds.Registration.CommandHandlers;

public class RegisterGuildCommandHandlerTests
{
    private readonly Mock<IUserGuildsRepository> _userGuilds   = new();
    private readonly Mock<IGuildsRepository>     _guilds       = new();
    private readonly Mock<IDiscordBotService>    _bot          = new();
    private readonly Mock<IGuildService>         _guildService = new();
    private readonly Mock<IAuditLogService>      _auditLog     = new();
    private readonly RegisterGuildCommandHandler _sut;

    private const string GuildId     = "guild-1";
    private const string RequesterId = "user-1";

    private static readonly RegisterGuildCommand Command = new()
    {
        GuildId = GuildId, RequesterDiscordId = RequesterId
    };

    public RegisterGuildCommandHandlerTests()
    {
        _bot.Setup(b => b.Guilds).Returns(_guildService.Object);
        _sut = new RegisterGuildCommandHandler(_userGuilds.Object, _guilds.Object, _bot.Object, _auditLog.Object);
    }

    [Fact]
    public async Task HandleAsync_RequesterNotMember_ReturnsForbidden()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(RequesterId, default))
            .ReturnsAsync([]);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_RequesterNotAdmin_ReturnsForbidden()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(RequesterId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = RequesterId, IsAdmin = false }]);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_BotNotInGuild_ReturnsBotNotPresent()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(RequesterId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = RequesterId, IsAdmin = true }]);

        _guildService.Setup(g => g.Get(GuildId, default))
            .Throws<InvalidOperationException>();

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildBotNotPresent);
    }

    [Fact]
    public async Task HandleAsync_GuildNotFoundInDb_ReturnsGuildNotFound()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(RequesterId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = RequesterId, IsAdmin = true }]);

        _guilds.Setup(g => g.RegisterAsync(GuildId, default)).ReturnsAsync(false);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildNotFound);
    }

    [Fact]
    public async Task HandleAsync_Success_ReturnsOk()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(RequesterId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = RequesterId, IsAdmin = true }]);

        _guilds.Setup(g => g.RegisterAsync(GuildId, default)).ReturnsAsync(true);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _guilds.Verify(g => g.RegisterAsync(GuildId, default), Times.Once);
    }
}
