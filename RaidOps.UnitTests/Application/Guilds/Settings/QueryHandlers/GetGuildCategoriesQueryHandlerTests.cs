using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Guilds.Settings.Queries;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Guilds.Settings.QueryHandlers;
using RaidOps.Domain.Enums;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

namespace RaidOps.UnitTests.Application.Guilds.Settings.QueryHandlers;

public class GetGuildCategoriesQueryHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IGuildService> _guildService = new();
    private readonly Mock<IDiscordBotService> _discordBotService = new();
    private readonly GetGuildCategoriesQueryHandler _sut;

    private const string GuildId = "guild-1";
    private const string RequesterId = "user-1";

    private static readonly GetGuildCategoriesQuery Query = new() { GuildId = GuildId, RequesterDiscordId = RequesterId };

    public GetGuildCategoriesQueryHandlerTests()
    {
        _discordBotService.Setup(d => d.Guilds).Returns(_guildService.Object);
        _sut = new GetGuildCategoriesQueryHandler(_access.Object, _discordBotService.Object);
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
        _guildService.Setup(s => s.GetCategories(GuildId, default)).Throws<InvalidOperationException>();

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildBotNotPresent);
    }

    [Fact]
    public async Task HandleAsync_Success_MapsCategoriesAndRootFlag()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guildService.Setup(s => s.GetCategories(GuildId, default)).Returns(new DiscordCategoriesInfo(
            true,
            [
                new DiscordCategoryInfo(111, "Raids", true),
                new DiscordCategoryInfo(222, "Officers", false),
            ]));

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.CanCreateRootChannel.Should().BeTrue();
        result.Value.Categories.Should().HaveCount(2);
        result.Value.Categories.Should().ContainSingle(c => c.Id == "111" && c.Name == "Raids" && c.CanCreateChannel);
        result.Value.Categories.Should().ContainSingle(c => c.Id == "222" && c.Name == "Officers" && !c.CanCreateChannel);
    }

    [Fact]
    public async Task HandleAsync_Success_CanCreateRootChannelFalse_SurvivesMapping()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guildService.Setup(s => s.GetCategories(GuildId, default)).Returns(new DiscordCategoriesInfo(false, []));

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.CanCreateRootChannel.Should().BeFalse();
        result.Value.Categories.Should().BeEmpty();
    }
}
