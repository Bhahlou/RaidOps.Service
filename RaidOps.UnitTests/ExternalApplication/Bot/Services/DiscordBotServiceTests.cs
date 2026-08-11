using FluentAssertions;
using Moq;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.ExternalApplication.Implementations.Bot.Services;
using RaidOps.UnitTests.ExternalApplication.Bot;

namespace RaidOps.UnitTests.ExternalApplication.Bot.Services;

public class DiscordBotServiceTests
{
    [Fact]
    public void Constructor_InitializesGuildsAndMessages()
    {
        var cache  = NetCordTestHelpers.EmptyCache();
        var client = NetCordTestHelpers.MakeGatewayClient(cache.Object);
        var emojiService = new Mock<IEmojiService>();

        var sut = new DiscordBotService(client, emojiService.Object);

        sut.Guilds.Should().NotBeNull().And.BeAssignableTo<IGuildService>();
        sut.Messages.Should().NotBeNull().And.BeAssignableTo<IMessageService>();
        sut.Emojis.Should().BeSameAs(emojiService.Object);
    }
}
