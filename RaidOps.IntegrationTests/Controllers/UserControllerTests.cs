using FluentAssertions;
using RaidOps.Application.Contracts.Authentication.Responses;
using RaidOps.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;

namespace RaidOps.IntegrationTests.Controllers;

public class UserControllerTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private const string Url = "/api/v1/user/me";
    private const string DiscordId = "200000000000000001";

    [Fact]
    public async Task GetMe_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync(Url);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMe_WhenUserNotInDb_Returns400()
    {
        var client = CreateAuthenticatedClient(discordId: "200000000000000099");

        var response = await client.GetAsync(Url);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetMe_WhenUserExists_ReturnsProfile()
    {
        await SeedAsync(db => { db.Users.Add(TestDataBuilder.CreateUser(DiscordId, "Bhahlou")); return Task.CompletedTask; });
        var client = CreateAuthenticatedClient(discordId: DiscordId, username: "Bhahlou");

        var response = await client.GetAsync(Url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        body!.DiscordId.Should().Be(DiscordId);
        body.Name.Should().Be("Bhahlou");
        body.Guilds.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMe_WhenUserHasRegisteredGuild_ReturnsGuildInResponse()
    {
        const string id = "200000000000000002";
        const string guildId = "800000000000000001";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: false));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync(Url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        body!.Guilds.Should().ContainSingle(g => g.Id == guildId && !g.IsAdmin && g.IsRegistered);
    }

    [Fact]
    public async Task GetMe_WhenUserIsAdminOfUnregisteredGuild_ReturnsGuildInResponse()
    {
        const string id = "200000000000000003";
        const string guildId = "800000000000000002";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: false));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync(Url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        body!.Guilds.Should().ContainSingle(g => g.Id == guildId && g.IsAdmin && !g.IsRegistered);
    }
}
