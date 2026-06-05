using System.Net;
using System.Web;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using RaidOps.ExternalApplication.Implementations.BNet;
using RaidOps.UnitTests.Helpers;

namespace RaidOps.UnitTests.ExternalApplication.BNet;

public class BnetApiServiceTests
{
    private readonly Mock<IConfiguration> _config = new();

    public BnetApiServiceTests()
    {
        _config.Setup(c => c["BattleNet:ClientId"]).Returns("bnet-client-id");
        _config.Setup(c => c["BattleNet:ClientSecret"]).Returns("bnet-client-secret");
    }

    // ── BuildAuthorizationUrl ─────────────────────────────────────────────────

    [Fact]
    public void BuildAuthorizationUrl_ContainsRequiredQueryParameters()
    {
        var sut = MakeSut(HttpStatusCode.OK);

        var url = sut.BuildAuthorizationUrl("eu", "https://app/callback", "state-token");

        var query = HttpUtility.ParseQueryString(new Uri(url).Query);
        query["client_id"].Should().Be("bnet-client-id");
        query["redirect_uri"].Should().Be("https://app/callback");
        query["response_type"].Should().Be("code");
        query["scope"].Should().Be("wow.profile");
        query["state"].Should().Be("state-token");
    }

    [Fact]
    public void BuildAuthorizationUrl_UsesRegionSubdomain()
    {
        var sut = MakeSut(HttpStatusCode.OK);

        var url = sut.BuildAuthorizationUrl("us", "https://app/callback", "state");

        url.Should().StartWith("https://us.battle.net");
    }

    // ── ExchangeCodeAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task ExchangeCodeAsync_Success_ReturnsDeserializedToken()
    {
        var json = """{"access_token":"bnet-tok","token_type":"Bearer","expires_in":86400,"scope":"wow.profile"}""";
        var sut  = MakeSut(HttpStatusCode.OK, json);

        var result = await sut.ExchangeCodeAsync("auth-code", "https://app/callback", "eu");

        result.AccessToken.Should().Be("bnet-tok");
        result.ExpiresIn.Should().Be(86400);
    }

    [Fact]
    public async Task ExchangeCodeAsync_SetsBasicAuthHeader()
    {
        var json    = """{"access_token":"tok","token_type":"Bearer","expires_in":86400,"scope":"wow.profile"}""";
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, json);
        var sut     = new BnetApiService(new HttpClient(handler), _config.Object);

        await sut.ExchangeCodeAsync("code", "https://app/callback", "eu");

        handler.LastRequest!.Headers.Authorization!.Scheme.Should().Be("Basic");
    }

    [Fact]
    public async Task ExchangeCodeAsync_NullJsonBody_ThrowsInvalidOperationException()
    {
        var sut = MakeSut(HttpStatusCode.OK, "null");

        var act = () => sut.ExchangeCodeAsync("code", "https://app/callback", "eu");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*deserialize*");
    }

    [Fact]
    public async Task ExchangeCodeAsync_NonSuccessStatus_ThrowsHttpRequestException()
    {
        var sut = MakeSut(HttpStatusCode.Unauthorized);

        var act = () => sut.ExchangeCodeAsync("bad-code", "https://app/callback", "eu");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    // ── GetUserInfoAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetUserInfoAsync_Success_ReturnsUserInfo()
    {
        var json = """{"id":42,"battletag":"Player#1234","sub":"42"}""";
        var sut  = MakeSut(HttpStatusCode.OK, json);

        var result = await sut.GetUserInfoAsync("tok", "eu");

        result.Id.Should().Be(42);
        result.BattleTag.Should().Be("Player#1234");
    }

    [Fact]
    public async Task GetUserInfoAsync_NullJsonBody_ThrowsInvalidOperationException()
    {
        var sut = MakeSut(HttpStatusCode.OK, "null");

        var act = () => sut.GetUserInfoAsync("tok", "eu");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*deserialize*");
    }

    // ── GetWowCharactersAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetWowCharactersAsync_Success_ReturnsResponse()
    {
        var json = """{"wow_accounts":[{"id":1,"characters":[]}]}""";
        var sut  = MakeSut(HttpStatusCode.OK, json);

        var result = await sut.GetWowCharactersAsync("tok", "eu", "profile-eu");

        result.WowAccounts.Should().ContainSingle(a => a.Id == 1);
    }

    [Fact]
    public async Task GetWowCharactersAsync_UrlContainsNamespaceAndLocale()
    {
        var json    = """{"wow_accounts":[]}""";
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, json);
        var sut     = new BnetApiService(new HttpClient(handler), _config.Object);

        await sut.GetWowCharactersAsync("tok", "eu", "profile-eu");

        var url = handler.LastRequest!.RequestUri!.ToString();
        url.Should().Contain("namespace=profile-eu");
        url.Should().Contain("locale=en_US");
    }

    [Fact]
    public async Task GetWowCharactersAsync_NullJsonBody_ThrowsInvalidOperationException()
    {
        var sut = MakeSut(HttpStatusCode.OK, "null");

        var act = () => sut.GetWowCharactersAsync("tok", "eu", "profile-eu");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*deserialize*");
    }

    // ── GetCharacterAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetCharacterAsync_CharacterNameLowercasedInUrl()
    {
        var json    = """{"level":80,"average_item_level":600,"equipped_item_level":590}""";
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, json);
        var sut     = new BnetApiService(new HttpClient(handler), _config.Object);

        await sut.GetCharacterAsync("tok", "eu", "profile-eu", "kazzak", "ARTHAS");

        var url = handler.LastRequest!.RequestUri!.ToString();
        url.Should().Contain("/arthas");
        url.Should().NotContain("/ARTHAS");
    }

    [Fact]
    public async Task GetCharacterMediaAsync_Success_ReturnsResponse()
    {
        var json    = """{"assets":[{"key":"avatar","value":"https://cdn/avatar.jpg"}]}""";
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, json);
        var sut     = new BnetApiService(new HttpClient(handler), _config.Object);

        var result = await sut.GetCharacterMediaAsync("tok", "eu", "profile-eu", "kazzak", "arthas");

        result.Assets.Should().ContainSingle(a => a.Key == "avatar");
        handler.LastRequest!.RequestUri!.ToString().Should().Contain("character-media");
    }

    [Fact]
    public async Task GetCharacterSpecializationsAsync_Success_ReturnsResponse()
    {
        var json    = """{"specializations":[],"specialization_groups":[]}""";
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, json);
        var sut     = new BnetApiService(new HttpClient(handler), _config.Object);

        var result = await sut.GetCharacterSpecializationsAsync("tok", "eu", "profile-eu", "kazzak", "arthas");

        result.Should().NotBeNull();
        handler.LastRequest!.RequestUri!.ToString().Should().Contain("specializations");
    }

    [Fact]
    public async Task GetCharacterAsync_NullJsonBody_ThrowsInvalidOperationException()
    {
        // GetProfileAsync<T> is shared by GetCharacterAsync, GetCharacterMediaAsync
        // and GetCharacterSpecializationsAsync — one test covers all three.
        var sut = MakeSut(HttpStatusCode.OK, "null");

        var act = () => sut.GetCharacterAsync("tok", "eu", "profile-eu", "kazzak", "arthas");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*deserialize*");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private BnetApiService MakeSut(HttpStatusCode status, string? content = null)
    {
        var handler = new FakeHttpMessageHandler(status, content);
        return new BnetApiService(new HttpClient(handler), _config.Object);
    }
}
