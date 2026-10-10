using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Guilds.Registration.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Guilds.Registration.CommandHandlers;
using RaidOps.Domain.Enums;
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
        _sut = new RegisterGuildCommandHandler(_userGuilds.Object, _guilds.Object, _bot.Object, _auditLog.Object, NullLogger<RegisterGuildCommandHandler>.Instance);
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

        _guildService.Setup(g => g.GetPreferredLocale(GuildId, default))
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

        _guilds.Setup(g => g.RegisterAsync(GuildId, It.IsAny<string?>(), default)).ReturnsAsync((Guild?)null);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildNotFound);
    }

    [Fact]
    public async Task HandleAsync_Success_ReturnsOk()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(RequesterId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = RequesterId, IsAdmin = true }]);

        _guilds.Setup(g => g.RegisterAsync(GuildId, It.IsAny<string?>(), default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test Guild", IconHash = "icon123" });

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _guilds.Verify(g => g.RegisterAsync(GuildId, It.IsAny<string?>(), default), Times.Once);
    }

    [Theory]
    [InlineData("fr", "fr")]
    [InlineData("de-DE", "de")]
    [InlineData("pt-BR", "en")]
    [InlineData(null, "en")]
    public async Task HandleAsync_Success_MapsDiscordPreferredLocaleToAppLanguage(string? discordLocale, string expectedLanguage)
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(RequesterId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = RequesterId, IsAdmin = true }]);
        _guildService.Setup(g => g.GetPreferredLocale(GuildId, default)).Returns(discordLocale);
        _guilds.Setup(g => g.RegisterAsync(GuildId, It.IsAny<string?>(), default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test Guild" });

        await _sut.HandleAsync(Command);

        _guilds.Verify(g => g.RegisterAsync(GuildId, expectedLanguage, default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Success_LogsGuildNameAndIconHash()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(RequesterId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = RequesterId, IsAdmin = true }]);

        _guilds.Setup(g => g.RegisterAsync(GuildId, It.IsAny<string?>(), default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test Guild", IconHash = "icon123" });

        await _sut.HandleAsync(Command);

        _auditLog.Verify(a => a.LogAsync(
            GuildId,
            RequesterId,
            GuildAuditAction.GuildRegistered,
            It.Is<Dictionary<string, string>>(v => v["guildName"] == "Test Guild" && v["guildIconHash"] == "icon123"),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Success_GuildHasNoIcon_OmitsIconHashVariable()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(RequesterId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = RequesterId, IsAdmin = true }]);

        _guilds.Setup(g => g.RegisterAsync(GuildId, It.IsAny<string?>(), default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test Guild", IconHash = null });

        await _sut.HandleAsync(Command);

        _auditLog.Verify(a => a.LogAsync(
            GuildId,
            RequesterId,
            GuildAuditAction.GuildRegistered,
            It.Is<Dictionary<string, string>>(v => v["guildName"] == "Test Guild" && !v.ContainsKey("guildIconHash")),
            default), Times.Once);
    }
}
