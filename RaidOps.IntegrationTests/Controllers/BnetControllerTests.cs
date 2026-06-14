using FluentAssertions;
using RaidOps.Application.Contracts.Characters.Responses;
using RaidOps.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;

namespace RaidOps.IntegrationTests.Controllers;

[Collection("Integration")]
public class BnetControllerTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private const string DiscordId = "400000000000000001";

    // ── Auth enforcement ────────────────────────────────────────────────────

    [Fact]
    public async Task GetAccount_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/bnet/account");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Initiate_WithoutToken_Returns401()
    {
        var response = await CreateNonRedirectingClient().GetAsync("/api/v1/bnet/link/initiate?region=eu");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Callback_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/bnet/link/callback?code=abc&state=xyz");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Business logic ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetAccount_WhenNoAccountLinked_Returns404()
    {
        const string id = "400000000000000002";
        await SeedAsync(db => { db.Users.Add(TestDataBuilder.CreateUser(id)); return Task.CompletedTask; });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync("/api/v1/bnet/account");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAccount_WhenAccountLinked_ReturnsAccountData()
    {
        const string id = "400000000000000003";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.BattleNetAccounts.Add(TestDataBuilder.CreateBnetAccount(id, "Bhahlou#1234"));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync("/api/v1/bnet/account");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BnetAccountResponse>();
        body!.BattleTag.Should().Be("Bhahlou#1234");
        body.Region.Should().Be("eu");
    }

    [Fact]
    public async Task Initiate_WithInvalidRegion_Returns400()
    {
        const string id = "400000000000000004";
        await SeedAsync(db => { db.Users.Add(TestDataBuilder.CreateUser(id)); return Task.CompletedTask; });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync("/api/v1/bnet/link/initiate?region=xx");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Initiate_WithValidRegion_RedirectsToBattleNet()
    {
        const string id = "400000000000000005";
        await SeedAsync(db => { db.Users.Add(TestDataBuilder.CreateUser(id)); return Task.CompletedTask; });
        var client = CreateAuthenticatedNonRedirectingClient(discordId: id);

        var response = await client.GetAsync("/api/v1/bnet/link/initiate?region=eu");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.Host.Should().Contain("battle.net");
    }

    [Fact]
    public async Task Callback_WithNullCodeOrState_RedirectsWithInvalidRequest()
    {
        const string id = "400000000000000006";
        await SeedAsync(db => { db.Users.Add(TestDataBuilder.CreateUser(id)); return Task.CompletedTask; });
        var client = CreateAuthenticatedNonRedirectingClient(discordId: id);

        var response = await client.GetAsync("/api/v1/bnet/link/callback");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("error=InvalidRequest");
    }

    [Fact]
    public async Task Callback_WithInvalidState_RedirectsWithInvalidStateError()
    {
        const string id = "400000000000000007";
        await SeedAsync(db => { db.Users.Add(TestDataBuilder.CreateUser(id)); return Task.CompletedTask; });
        var client = CreateAuthenticatedNonRedirectingClient(discordId: id);

        var response = await client.GetAsync("/api/v1/bnet/link/callback?code=anycode&state=tampered-state");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("error=InvalidState");
    }

    [Fact]
    public async Task Callback_WithValidState_LinksAccountAndRedirects()
    {
        const string id = "400000000000000008";
        await SeedAsync(db => { db.Users.Add(TestDataBuilder.CreateUser(id)); return Task.CompletedTask; });
        var state = TestTokenBuilder.CreateBnetStateToken(id, "eu");
        var client = CreateAuthenticatedNonRedirectingClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/bnet/link/callback?code=stub-code&state={state}");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("bnet_linked=true");
    }
}
