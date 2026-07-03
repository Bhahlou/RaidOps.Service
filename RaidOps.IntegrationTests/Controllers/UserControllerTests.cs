using FluentAssertions;
using RaidOps.Application.Contracts.Authentication.Responses;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RaidOps.IntegrationTests.Controllers;

[Collection("Integration")]
public class UserControllerTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private const string Url = "/api/v1/user/me";
    private const string DiscordId = "200000000000000001";

    // The API serializes enums as strings (see Program.cs's JsonStringEnumConverter registration)
    // but HttpContent.ReadFromJsonAsync defaults to numeric enum parsing unless told otherwise.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

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
        const string guildId = "850000000000000001";
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
        var body = await response.Content.ReadFromJsonAsync<UserResponse>(JsonOptions);
        body!.Guilds.Should().ContainSingle(g => g.Id == guildId && !g.IsAdmin && g.IsRegistered);
        body.Guilds.Single().AccessLevel.Should().Be(GuildAccessLevel.Public);
    }

    [Fact]
    public async Task GetMe_WhenUserIsAdminOfUnregisteredGuild_ReturnsGuildInResponse()
    {
        const string id = "200000000000000003";
        const string guildId = "850000000000000002";
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
        var body = await response.Content.ReadFromJsonAsync<UserResponse>(JsonOptions);
        body!.Guilds.Should().ContainSingle(g => g.Id == guildId && g.IsAdmin && !g.IsRegistered);
        body.Guilds.Single().AccessLevel.Should().Be(GuildAccessLevel.Officer);
    }

    // ── Notifications ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetMe_AdminOfConfiguredGuildWithoutOfficerThreshold_ReturnsNotification()
    {
        const string id = "200000000000000004";
        const string guildId = "850000000000000003";
        await SeedAsync(db =>
        {
            var guild = TestDataBuilder.CreateGuild(guildId, isRegistered: true);
            guild.Timezone = "Europe/Paris";
            guild.RosterMode = RosterMode.Open;
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(guild);
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync(Url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>(JsonOptions);
        body!.Notifications.Should().ContainSingle(n =>
            n.Type == NotificationType.OfficerThresholdNotConfigured && n.GuildId == guildId);
    }

    [Fact]
    public async Task GetMe_AdminOfGuildWithOfficerThreshold_NoNotification()
    {
        const string id = "200000000000000005";
        const string guildId = "850000000000000004";
        await SeedAsync(db =>
        {
            var guild = TestDataBuilder.CreateGuild(guildId, isRegistered: true);
            guild.Timezone = "Europe/Paris";
            guild.RosterMode = RosterMode.Open;
            guild.MinOfficerRoleId = "999000000000000001";
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(guild);
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync(Url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>(JsonOptions);
        body!.Notifications.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMe_AdminOfGuildStillOnboarding_NoNotification()
    {
        // Bot invited but settings step not completed yet — never nudge mid get-started.
        const string id = "200000000000000006";
        const string guildId = "850000000000000005";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync(Url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>(JsonOptions);
        body!.Notifications.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMe_AdminDismissedNotification_NoNotification()
    {
        const string id = "200000000000000008";
        const string guildId = "850000000000000007";
        await SeedAsync(db =>
        {
            var guild = TestDataBuilder.CreateGuild(guildId, isRegistered: true);
            guild.Timezone = "Europe/Paris";
            guild.RosterMode = RosterMode.Open;
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(guild);
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            db.NotificationDismissals.Add(new NotificationDismissal
            {
                UserDiscordId = id,
                Type = NotificationType.OfficerThresholdNotConfigured,
                GuildId = guildId,
                DismissedAt = DateTime.UtcNow,
            });
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync(Url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>(JsonOptions);
        body!.Notifications.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMe_NonAdminOfConfiguredGuildWithoutOfficerThreshold_NoNotification()
    {
        const string id = "200000000000000007";
        const string guildId = "850000000000000006";
        await SeedAsync(db =>
        {
            var guild = TestDataBuilder.CreateGuild(guildId, isRegistered: true);
            guild.Timezone = "Europe/Paris";
            guild.RosterMode = RosterMode.Open;
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(guild);
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: false));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync(Url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>(JsonOptions);
        body!.Notifications.Should().BeEmpty();
    }
}
