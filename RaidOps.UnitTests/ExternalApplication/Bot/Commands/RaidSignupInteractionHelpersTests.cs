using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Settings.Queries;
using RaidOps.Application.Contracts.Guilds.Settings.Responses;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.ExternalApplication.Implementations.Bot.Commands;

namespace RaidOps.UnitTests.ExternalApplication.Bot.Commands;

public class RaidSignupInteractionHelpersTests
{
    private readonly Mock<IDiscordBotService> _discordBotService = new();
    private readonly Mock<IEmojiService> _emojiService = new();
    private readonly Mock<IQueryDispatcher> _queryDispatcher = new();

    public RaidSignupInteractionHelpersTests() =>
        _discordBotService.Setup(d => d.Emojis).Returns(_emojiService.Object);

    [Fact]
    public void SpecEmojiProperties_EmojiSynced_ReturnsCustomEmoji()
    {
        _emojiService.Setup(e => e.GetId("spec_warrior_arms")).Returns(111UL);

        var result = RaidSignupInteractionHelpers.SpecEmojiProperties(_discordBotService.Object, 1, "Arms");

        result.Should().NotBeNull();
    }

    [Fact]
    public void SpecEmojiProperties_EmojiNotSynced_ReturnsNull()
    {
        _emojiService.Setup(e => e.GetId(It.IsAny<string>())).Returns((ulong?)null);

        var result = RaidSignupInteractionHelpers.SpecEmojiProperties(_discordBotService.Object, 1, "Arms");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveLanguageAsync_SettingsNotFound_ReturnsEn()
    {
        _queryDispatcher
            .Setup(q => q.DispatchAsync<GetGuildSettingsQuery, GuildSettingsResponse>(It.IsAny<GetGuildSettingsQuery>(), default))
            .ReturnsAsync(Result<GuildSettingsResponse>.Fail(ResponseDetail.GuildNotFound, "not found"));

        var result = await RaidSignupInteractionHelpers.ResolveLanguageAsync(_queryDispatcher.Object, "guild-1", "42");

        result.Should().Be("en");
    }

    [Fact]
    public async Task ResolveLanguageAsync_LanguageNotSet_ReturnsEn()
    {
        _queryDispatcher
            .Setup(q => q.DispatchAsync<GetGuildSettingsQuery, GuildSettingsResponse>(It.IsAny<GetGuildSettingsQuery>(), default))
            .ReturnsAsync(Result<GuildSettingsResponse>.Ok(new GuildSettingsResponse { Language = null }));

        var result = await RaidSignupInteractionHelpers.ResolveLanguageAsync(_queryDispatcher.Object, "guild-1", "42");

        result.Should().Be("en");
    }

    [Fact]
    public async Task ResolveLanguageAsync_LanguageSet_ReturnsIt()
    {
        _queryDispatcher
            .Setup(q => q.DispatchAsync<GetGuildSettingsQuery, GuildSettingsResponse>(It.IsAny<GetGuildSettingsQuery>(), default))
            .ReturnsAsync(Result<GuildSettingsResponse>.Ok(new GuildSettingsResponse { Language = "fr" }));

        var result = await RaidSignupInteractionHelpers.ResolveLanguageAsync(_queryDispatcher.Object, "guild-1", "42");

        result.Should().Be("fr");
    }
}
