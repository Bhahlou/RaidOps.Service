using FluentAssertions;
using RaidOps.IntegrationTests.Infrastructure;
using System.Net;

namespace RaidOps.IntegrationTests.Controllers;

[Collection("Integration")]
public class DiscordAuthControllerTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    // ── No auth required ────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_Returns200()
    {
        var response = await Client.PostAsync("/api/v1/discordauth/logout", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Signup_RedirectsToDiscord()
    {
        var client = CreateNonRedirectingClient();

        var response = await client.GetAsync("/api/v1/discordauth/signup");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.Host.Should().Contain("discord.com");
    }

    // ── Refresh token flow ──────────────────────────────────────────────────

    [Fact]
    public async Task Refresh_WithoutCookie_Returns401()
    {
        var response = await Client.PostAsync("/api/v1/discordauth/refresh", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithInvalidRefreshToken_Returns401()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/discordauth/refresh");
        request.Headers.Add("Cookie", "refresh_token=not-a-valid-jwt");

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithValidTokenAndUserInDb_Returns200WithNewCookies()
    {
        const string id = "600000000000000001";
        await SeedAsync(db => { db.Users.Add(TestDataBuilder.CreateUser(id)); return Task.CompletedTask; });

        var refreshToken = TestTokenBuilder.CreateRefreshToken(id);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/discordauth/refresh");
        request.Headers.Add("Cookie", $"refresh_token={refreshToken}");

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.TryGetValues("Set-Cookie", out var cookies);
        cookies.Should().Contain(c => c.StartsWith("access_token="));
        cookies.Should().Contain(c => c.StartsWith("refresh_token="));
    }
}
