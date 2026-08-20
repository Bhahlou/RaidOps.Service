using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Events.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Events.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

namespace RaidOps.UnitTests.Application.Raids.Events.CommandHandlers;

public class CreateRaidAnnouncementChannelCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IDiscordBotService> _discordBotService = new();
    private readonly Mock<IGuildService> _guildService = new();
    private readonly CreateRaidAnnouncementChannelCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "officer-1";

    public CreateRaidAnnouncementChannelCommandHandlerTests()
    {
        _discordBotService.Setup(d => d.Guilds).Returns(_guildService.Object);
        _sut = new CreateRaidAnnouncementChannelCommandHandler(_access.Object, _discordBotService.Object);
    }

    private static CreateRaidAnnouncementChannelCommand MakeCommand(string? categoryId = null) => new()
    {
        GuildId = GuildId,
        RequesterDiscordId = RequesterId,
        GuildBranchId = GuildBranchId,
        Name = "kara-tue-18-aug",
        CategoryId = categoryId,
    };

    [Fact]
    public async Task HandleAsync_NotOfficer_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
        _guildService.Verify(g => g.CreateTextChannelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Success_CreatesChannelAndReturnsItsDetails()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guildService.Setup(g => g.CreateTextChannelAsync(GuildId, "kara-tue-18-aug", "cat-1", default))
            .ReturnsAsync(new DiscordChannelInfo(555, "kara-tue-18-aug", [], "Raids"));

        var result = await _sut.HandleAsync(MakeCommand(categoryId: "cat-1"));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Body.Should().BeEquivalentTo(new { Id = "555", Name = "kara-tue-18-aug", MissingPermissions = Array.Empty<DiscordChannelPermissionFlag>(), CategoryName = "Raids" });
    }

    [Fact]
    public async Task HandleAsync_BotNotInGuild_ReturnsGuildBotNotPresent()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guildService.Setup(g => g.CreateTextChannelAsync(GuildId, It.IsAny<string>(), It.IsAny<string?>(), default))
            .ThrowsAsync(new InvalidOperationException("not present"));

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildBotNotPresent);
    }

    [Fact]
    public async Task HandleAsync_DiscordCallFails_ReturnsDiscordChannelCreationFailed()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guildService.Setup(g => g.CreateTextChannelAsync(GuildId, It.IsAny<string>(), It.IsAny<string?>(), default))
            .ThrowsAsync(new Exception("403 Forbidden"));

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.DiscordChannelCreationFailed);
        result.Detail.Should().Contain("403 Forbidden");
    }
}
