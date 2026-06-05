using FluentAssertions;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.ExternalApplication.Implementations.Bot;

namespace RaidOps.UnitTests.ExternalApplication.Bot;

public class DiscordBotServiceTests
{
    [Fact]
    public void Constructor_InitializesGuildsAndMessages()
    {
        var cache  = NetCordTestHelpers.EmptyCache();
        var client = NetCordTestHelpers.MakeGatewayClient(cache.Object);

        var sut = new DiscordBotService(client);

        sut.Guilds.Should().NotBeNull().And.BeAssignableTo<IGuildService>();
        sut.Messages.Should().NotBeNull().And.BeAssignableTo<IMessageService>();
    }
}
