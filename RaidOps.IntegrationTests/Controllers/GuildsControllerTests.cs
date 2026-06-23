using FluentAssertions;
using RaidOps.Infrastructure.Persistence.Implementations;
using RaidOps.IntegrationTests.Infrastructure;
using System.Net;

namespace RaidOps.IntegrationTests.Controllers;

[Collection("Integration")]
public class GuildsControllerTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private const string DiscordId = "500000000000000001";

    // ── Auth enforcement ────────────────────────────────────────────────────

    [Fact]
    public async Task Initiate_WithoutToken_Returns401()
    {
        var client = CreateNonRedirectingClient();

        var response = await client.GetAsync("/api/v1/guilds/register/initiate?guildId=123456789012345678");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Callback_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/guilds/register/callback?guild_id=123&state=invalid");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Business logic ──────────────────────────────────────────────────────

    [Fact]
    public async Task Initiate_WithToken_RedirectsToDiscordBotAuthPage()
    {
        var client = CreateAuthenticatedNonRedirectingClient(discordId: DiscordId);

        var response = await client.GetAsync("/api/v1/guilds/register/initiate?guildId=123456789012345678");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.Host.Should().Contain("discord.com");
    }

    [Fact]
    public async Task Callback_WithInvalidState_RedirectsToFrontendWithError()
    {
        var client = CreateAuthenticatedNonRedirectingClient(discordId: DiscordId);

        var response = await client.GetAsync("/api/v1/guilds/register/callback?guild_id=123&state=tampered-state");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("error=invalid_state");
    }

    [Fact]
    public async Task Callback_WhenUserNotAdmin_RedirectsWithRegisterFailed()
    {
        const string id = "500000000000000002";
        const string guildId = "900000000000000001";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: false));
            return Task.CompletedTask;
        });
        var state = TestTokenBuilder.CreateStateToken(guildId, id);
        var client = CreateAuthenticatedNonRedirectingClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/register/callback?guild_id={guildId}&state={state}");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("error=register_failed");
    }

    [Fact]
    public async Task Callback_WhenUserIsAdmin_RedirectsToGuildDashboard()
    {
        const string id = "500000000000000003";
        const string guildId = "900000000000000002";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            return Task.CompletedTask;
        });
        var state = TestTokenBuilder.CreateStateToken(guildId, id);
        var client = CreateAuthenticatedNonRedirectingClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/register/callback?guild_id={guildId}&state={state}");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain($"/guild-register/{guildId}");
    }

    [Fact]
    public async Task Callback_WithGetStartedReturnTo_RedirectsToGetStarted()
    {
        const string id = "500000000000000004";
        const string guildId = "900000000000000003";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            return Task.CompletedTask;
        });
        var state = TestTokenBuilder.CreateStateToken(guildId, id, returnTo: "get-started");
        var client = CreateAuthenticatedNonRedirectingClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/register/callback?guild_id={guildId}&state={state}");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("/get-started");
        response.Headers.Location!.ToString().Should().NotContain("/guild-register");
    }

    [Fact]
    public async Task Callback_CancelledWithGetStartedReturnTo_RedirectsToGetStartedNotNoGuild()
    {
        const string id = "500000000000000005";
        const string guildId = "900000000000000004";
        var state = TestTokenBuilder.CreateStateToken(guildId, id, returnTo: "get-started");
        var client = CreateAuthenticatedNonRedirectingClient(discordId: id);

        // Discord omits guild_id when the bot-invite consent screen is cancelled.
        var response = await client.GetAsync($"/api/v1/guilds/register/callback?state={state}");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("/get-started");
        response.Headers.Location!.ToString().Should().NotContain("/no-guild");
    }
}
