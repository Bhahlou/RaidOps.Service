using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NetCord.Rest;
using RaidOps.ExternalApplication.Implementations.Bot;

namespace RaidOps.UnitTests.ExternalApplication.Bot;

public class EmojiServiceTests
{
    private const string ApplicationJson = """{"id":"999"}""";

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    /// <summary>Every image download succeeds with a fake 3-byte "image" unless overridden.</summary>
    private static Mock<IHttpClientFactory> MakeHttpClientFactory(Func<HttpRequestMessage, HttpResponseMessage>? respond = null)
    {
        var handler = new StubHttpMessageHandler(respond ?? (_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) }));
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler));
        return factory;
    }

    /// <summary>
    /// Builds the (RestClient, IRestRequestHandler mock) pair and an EmojiService wired to it in
    /// one step — every test needs both since NetCordTestHelpers.MakeFakeRestClient() is the only
    /// way to get a working RestClient without a live gateway connection.
    /// </summary>
    private static (EmojiService Sut, Mock<IRestRequestHandler> RestHandler) MakeSutWithRest(Mock<IHttpClientFactory>? httpClientFactory = null)
    {
        var (rest, restHandler) = NetCordTestHelpers.MakeFakeRestClient();
        var cache = NetCordTestHelpers.EmptyCache();
        var client = NetCordTestHelpers.MakeGatewayClient(cache.Object, rest);
        var sut = new EmojiService(client, (httpClientFactory ?? MakeHttpClientFactory()).Object, new Mock<ILogger<EmojiService>>().Object);
        return (sut, restHandler);
    }

    // ── GetMarkdown ───────────────────────────────────────────────────────────

    [Fact]
    public void GetMarkdown_UnknownName_ReturnsNull()
    {
        var (sut, _) = MakeSutWithRest();

        sut.GetMarkdown("class_warrior").Should().BeNull();
    }

    // ── SyncAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SyncAsync_NewEntry_CreatesItAndCachesTheReturnedId()
    {
        var (sut, restHandler) = MakeSutWithRest();
        var callCount = 0;
        restHandler.Setup(h => h.SendAsync(It.IsAny<HttpRequestMessage>(), default)).ReturnsAsync(() =>
        {
            callCount++;
            return callCount switch
            {
                1 => NetCordTestHelpers.JsonResponse(ApplicationJson),
                2 => NetCordTestHelpers.JsonResponse("""{"items":[]}"""),
                _ => NetCordTestHelpers.JsonResponse("""{"id":"111","name":"class_warrior"}"""),
            };
        });

        await sut.SyncAsync([("class_warrior", "https://cdn.example.com/warrior.jpg")]);

        restHandler.Verify(h => h.SendAsync(It.IsAny<HttpRequestMessage>(), default), Times.Exactly(3));
        sut.GetMarkdown("class_warrior").Should().Be("<:class_warrior:111>");
    }

    [Fact]
    public async Task SyncAsync_EntryAlreadySynced_SkipsUploadAndCachesTheExistingId()
    {
        var (sut, restHandler) = MakeSutWithRest();
        var callCount = 0;
        restHandler.Setup(h => h.SendAsync(It.IsAny<HttpRequestMessage>(), default)).ReturnsAsync(() =>
        {
            callCount++;
            return callCount switch
            {
                1 => NetCordTestHelpers.JsonResponse(ApplicationJson),
                2 => NetCordTestHelpers.JsonResponse("""{"items":[{"id":"555","name":"class_warrior"}]}"""),
                _ => throw new InvalidOperationException("Should not upload an already-synced emoji."),
            };
        });

        await sut.SyncAsync([("class_warrior", "https://cdn.example.com/warrior.jpg")]);

        // Only the two read calls (current application + existing emoji list) — no create call.
        restHandler.Verify(h => h.SendAsync(It.IsAny<HttpRequestMessage>(), default), Times.Exactly(2));
        sut.GetMarkdown("class_warrior").Should().Be("<:class_warrior:555>");
    }

    [Fact]
    public async Task SyncAsync_MultipleNewEntries_UploadsEachAndCachesAllIds()
    {
        var (sut, restHandler) = MakeSutWithRest();
        var callCount = 0;
        restHandler.Setup(h => h.SendAsync(It.IsAny<HttpRequestMessage>(), default)).ReturnsAsync(() =>
        {
            callCount++;
            return callCount switch
            {
                1 => NetCordTestHelpers.JsonResponse(ApplicationJson),
                2 => NetCordTestHelpers.JsonResponse("""{"items":[]}"""),
                3 => NetCordTestHelpers.JsonResponse("""{"id":"111","name":"class_warrior"}"""),
                _ => NetCordTestHelpers.JsonResponse("""{"id":"222","name":"class_mage"}"""),
            };
        });

        await sut.SyncAsync(
        [
            ("class_warrior", "https://cdn.example.com/warrior.jpg"),
            ("class_mage", "https://cdn.example.com/mage.jpg"),
        ]);

        restHandler.Verify(h => h.SendAsync(It.IsAny<HttpRequestMessage>(), default), Times.Exactly(4));
        sut.GetMarkdown("class_warrior").Should().Be("<:class_warrior:111>");
        sut.GetMarkdown("class_mage").Should().Be("<:class_mage:222>");
    }

    [Fact]
    public async Task SyncAsync_ImageDownloadFails_SkipsThatEntryButStillSyncsTheRest()
    {
        var httpClientFactory = MakeHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var (sut, restHandler) = MakeSutWithRest(httpClientFactory);
        var callCount = 0;
        restHandler.Setup(h => h.SendAsync(It.IsAny<HttpRequestMessage>(), default)).ReturnsAsync(() =>
        {
            callCount++;
            return callCount switch
            {
                1 => NetCordTestHelpers.JsonResponse(ApplicationJson),
                2 => NetCordTestHelpers.JsonResponse("""{"items":[]}"""),
                _ => throw new InvalidOperationException("Should never reach CreateApplicationEmojiAsync when the download itself failed."),
            };
        });

        var act = () => sut.SyncAsync([("class_warrior", "https://cdn.example.com/dead-link.jpg")]);

        await act.Should().NotThrowAsync();
        // Only the two read calls — the download 404 must short-circuit before any create call.
        restHandler.Verify(h => h.SendAsync(It.IsAny<HttpRequestMessage>(), default), Times.Exactly(2));
        sut.GetMarkdown("class_warrior").Should().BeNull();
    }

    [Fact]
    public async Task SyncAsync_OneEntryFailsToUpload_StillSyncsTheOthers()
    {
        var (sut, restHandler) = MakeSutWithRest();
        var callCount = 0;
        restHandler.Setup(h => h.SendAsync(It.IsAny<HttpRequestMessage>(), default)).ReturnsAsync(() =>
        {
            callCount++;
            return callCount switch
            {
                1 => NetCordTestHelpers.JsonResponse(ApplicationJson),
                2 => NetCordTestHelpers.JsonResponse("""{"items":[]}"""),
                3 => NetCordTestHelpers.JsonResponse("""{"error":"boom"}""", HttpStatusCode.InternalServerError),
                _ => NetCordTestHelpers.JsonResponse("""{"id":"222","name":"class_mage"}"""),
            };
        });

        var act = () => sut.SyncAsync(
        [
            ("class_warrior", "https://cdn.example.com/warrior.jpg"),
            ("class_mage", "https://cdn.example.com/mage.jpg"),
        ]);

        await act.Should().NotThrowAsync();
        sut.GetMarkdown("class_warrior").Should().BeNull();
        sut.GetMarkdown("class_mage").Should().Be("<:class_mage:222>");
    }
}
