using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using RaidOps.ExternalApplication.Implementations.Services;
using RaidOps.UnitTests.Helpers;

namespace RaidOps.UnitTests.ExternalApplication.Services;

public class DiscordApiServiceTests
{
    private readonly Mock<IConfiguration> _config = new();

    public DiscordApiServiceTests()
    {
        _config.Setup(c => c["Discord:ClientId"]).Returns("client-id");
        _config.Setup(c => c["Discord:ClientSecret"]).Returns("client-secret");
    }

    // ── GetCurrentUserAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetCurrentUserAsync_Success_ReturnsDeserializedUser()
    {
        var json = """{"id":"123","global_name":"Bhahlou","avatar":"abc"}""";
        var sut  = MakeSut(HttpStatusCode.OK, json);

        var result = await sut.GetCurrentUserAsync("token");

        result.Id.Should().Be("123");
        result.Username.Should().Be("Bhahlou");
        result.Avatar.Should().Be("abc");
    }

    [Fact]
    public async Task GetCurrentUserAsync_SetsAuthorizationHeader()
    {
        var json    = """{"id":"1","global_name":"u","avatar":null}""";
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, json);
        var sut     = new DiscordApiService(new HttpClient(handler), _config.Object);

        await sut.GetCurrentUserAsync("my-token");

        handler.LastRequest!.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.LastRequest.Headers.Authorization.Parameter.Should().Be("my-token");
    }

    [Fact]
    public async Task GetCurrentUserAsync_NullJsonBody_ThrowsInvalidOperationException()
    {
        // "null" is valid JSON but Deserialize<T>("null") returns null for reference types
        var sut = MakeSut(HttpStatusCode.OK, "null");

        var act = () => sut.GetCurrentUserAsync("token");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*deserialize*");
    }

    [Fact]
    public async Task GetCurrentUserAsync_NonSuccessStatus_ThrowsHttpRequestException()
    {
        var sut = MakeSut(HttpStatusCode.Unauthorized);

        var act = () => sut.GetCurrentUserAsync("bad-token");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    // ── GetCurrentUserGuildsAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetCurrentUserGuildsAsync_NullJsonBody_ReturnsEmptyList()
    {
        var sut = MakeSut(HttpStatusCode.OK, "null");

        var result = await sut.GetCurrentUserGuildsAsync("token");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCurrentUserGuildsAsync_Success_ReturnsList()
    {
        var json = """[{"id":"g1","name":"Guild One","icon":null,"owner":true,"permissions":"8"}]""";
        var sut  = MakeSut(HttpStatusCode.OK, json);

        var result = await sut.GetCurrentUserGuildsAsync("token");

        result.Should().ContainSingle(g => g.Id == "g1" && g.Name == "Guild One");
    }

    [Fact]
    public async Task GetCurrentUserGuildsAsync_NonSuccessStatus_ThrowsHttpRequestException()
    {
        var sut = MakeSut(HttpStatusCode.Forbidden);

        var act = () => sut.GetCurrentUserGuildsAsync("token");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    // ── RefreshAccessTokenAsync ──────────────────────────────────────────────

    [Fact]
    public async Task RefreshAccessTokenAsync_Success_ReturnsTokenResponse()
    {
        var json = """{"access_token":"new-access","refresh_token":"new-refresh","expires_in":604800,"token_type":"Bearer","scope":"identify"}""";
        var sut  = MakeSut(HttpStatusCode.OK, json);

        var result = await sut.RefreshAccessTokenAsync("old-refresh");

        result.AccessToken.Should().Be("new-access");
        result.RefreshToken.Should().Be("new-refresh");
    }

    [Fact]
    public async Task RefreshAccessTokenAsync_NullJsonBody_ThrowsInvalidOperationException()
    {
        var sut = MakeSut(HttpStatusCode.OK, "null");

        var act = () => sut.RefreshAccessTokenAsync("old-refresh");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*deserialize*");
    }

    [Fact]
    public async Task RefreshAccessTokenAsync_NonSuccessStatus_ThrowsHttpRequestException()
    {
        var sut = MakeSut(HttpStatusCode.BadRequest);

        var act = () => sut.RefreshAccessTokenAsync("invalid");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private DiscordApiService MakeSut(HttpStatusCode status, string? content = null)
    {
        var handler = new FakeHttpMessageHandler(status, content);
        return new DiscordApiService(new HttpClient(handler), _config.Object);
    }
}
