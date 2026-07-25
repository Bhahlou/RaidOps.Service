using FluentAssertions;
using Moq;
using NetCord.Rest;
using RaidOps.ExternalApplication.Implementations.Bot;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

namespace RaidOps.UnitTests.ExternalApplication.Bot;

public class MessageServiceTests
{
    private const ulong ChannelId = 42UL;

    private const string MessageJson = """{"id":"1","channel_id":"42","content":"","type":0,"timestamp":"2025-01-01T00:00:00+00:00","edited_timestamp":null,"tts":false,"mention_everyone":false,"mentions":[],"mention_roles":[],"attachments":[],"embeds":[],"pinned":false,"author":{"id":"1","username":"bot","discriminator":"0","global_name":"bot","avatar":null}}""";

    private static MessageService MakeSut(out Mock<IRestRequestHandler> handler)
    {
        var (rest, h) = NetCordTestHelpers.MakeFakeRestClient();
        handler = h;
        handler.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), default))
            .ReturnsAsync(() => NetCordTestHelpers.JsonResponse(MessageJson));

        var cache = NetCordTestHelpers.EmptyCache();
        var client = NetCordTestHelpers.MakeGatewayClient(cache.Object, rest);
        return new MessageService(client);
    }

    [Fact]
    public async Task SendEmbedAsync_MinimalEmbed_SendsOneRequest()
    {
        var sut = MakeSut(out var handler);

        var act = () => sut.SendEmbedAsync(ChannelId, new DiscordEmbedContent("Title"));

        await act.Should().NotThrowAsync();
        handler.Verify(h => h.SendAsync(It.IsAny<HttpRequestMessage>(), default), Times.Once);
    }

    [Fact]
    public async Task SendEmbedAsync_FullEmbed_SendsOneRequest()
    {
        var sut = MakeSut(out var handler);
        var embed = new DiscordEmbedContent(
            Title: "New absence",
            Description: "<@1> declared an absence.",
            ColorHex: 0xFEE75C,
            Fields: [new DiscordEmbedField("Dates", "1/1/2026")],
            FooterText: "RaidOps",
            Url: "https://raidops.example/raids/1",
            Author: new DiscordEmbedAuthor("Bhahlou", "https://cdn.discordapp.com/avatars/1/abc.webp"));

        var act = () => sut.SendEmbedAsync(ChannelId, embed);

        await act.Should().NotThrowAsync();
        handler.Verify(h => h.SendAsync(It.IsAny<HttpRequestMessage>(), default), Times.Once);
    }

    [Fact]
    public async Task SendEmbedAsync_AuthorWithoutIconUrl_SendsOneRequest()
    {
        var sut = MakeSut(out var handler);
        var embed = new DiscordEmbedContent("Title", Author: new DiscordEmbedAuthor("Bhahlou"));

        var act = () => sut.SendEmbedAsync(ChannelId, embed);

        await act.Should().NotThrowAsync();
        handler.Verify(h => h.SendAsync(It.IsAny<HttpRequestMessage>(), default), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_CallsGetChannelThenSendMessage()
    {
        var (rest, handler) = NetCordTestHelpers.MakeFakeRestClient();

        // First call: GetChannelAsync → returns minimal channel JSON
        // Second call: SendMessageAsync → returns minimal message JSON
        const string channelJson = """{"id":"42","type":0}""";
        const string messageJson = """{"id":"1","channel_id":"42","content":"hello","type":0,"timestamp":"2025-01-01T00:00:00+00:00","edited_timestamp":null,"tts":false,"mention_everyone":false,"mentions":[],"mention_roles":[],"attachments":[],"embeds":[],"pinned":false,"author":{"id":"1","username":"bot","discriminator":"0","global_name":"bot","avatar":null}}""";

        var callCount = 0;
        handler.Setup(h => h.SendAsync(It.IsAny<HttpRequestMessage>(), default))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1
                    ? NetCordTestHelpers.JsonResponse(channelJson)
                    : NetCordTestHelpers.JsonResponse(messageJson);
            });

        var cache  = NetCordTestHelpers.EmptyCache();
        var client = NetCordTestHelpers.MakeGatewayClient(cache.Object, rest);
        var sut    = new MessageService(client);

        var act = () => sut.SendMessageAsync(ChannelId, "hello");

        await act.Should().NotThrowAsync();
        handler.Verify(h => h.SendAsync(It.IsAny<HttpRequestMessage>(), default), Times.Exactly(2));
    }
}
