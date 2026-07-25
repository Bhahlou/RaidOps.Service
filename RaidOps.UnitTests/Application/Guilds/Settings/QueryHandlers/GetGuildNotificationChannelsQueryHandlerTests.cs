using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Guilds.Settings.Queries;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Guilds.Settings.QueryHandlers;
using RaidOps.Domain.Enums;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

namespace RaidOps.UnitTests.Application.Guilds.Settings.QueryHandlers;

public class GetGuildNotificationChannelsQueryHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IGuildService> _guildService = new();
    private readonly Mock<IDiscordBotService> _discordBotService = new();
    private readonly GetGuildNotificationChannelsQueryHandler _sut;

    private const string GuildId = "guild-1";
    private const string RequesterId = "user-1";

    private static readonly GetGuildNotificationChannelsQuery Query = new() { GuildId = GuildId, RequesterDiscordId = RequesterId };

    public GetGuildNotificationChannelsQueryHandlerTests()
    {
        _discordBotService.Setup(d => d.Guilds).Returns(_guildService.Object);
        _sut = new GetGuildNotificationChannelsQueryHandler(_access.Object, _discordBotService.Object);
    }

    [Fact]
    public async Task HandleAsync_RequesterNotOfficer_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_BotNotInGuild_ReturnsGuildBotNotPresent()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guildService.Setup(s => s.GetChannels(GuildId, default)).Throws<InvalidOperationException>();

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildBotNotPresent);
    }

    [Fact]
    public async Task HandleAsync_Success_MapsChannelsToResponse()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guildService.Setup(s => s.GetChannels(GuildId, default)).Returns(
        [
            new DiscordChannelInfo(111, "general", true, "Text Channels"),
            new DiscordChannelInfo(222, "mod-only", false, null),
        ]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().ContainSingle(c => c.Id == "111" && c.Name == "general" && c.BotCanSendMessages && c.CategoryName == "Text Channels");
        result.Value.Should().ContainSingle(c => c.Id == "222" && c.Name == "mod-only" && !c.BotCanSendMessages && c.CategoryName == null);
    }
}
