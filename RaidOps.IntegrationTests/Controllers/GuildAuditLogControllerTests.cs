using FluentAssertions;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RaidOps.IntegrationTests.Controllers;

[Collection("Integration")]
public class GuildAuditLogControllerTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    // ── Auth enforcement ────────────────────────────────────────────────────

    [Fact]
    public async Task GetAuditLog_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/guilds/123456789012345678/audit-log");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Business logic ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetAuditLog_WhenUserNotAdmin_Returns400()
    {
        const string id      = "520000000000000001";
        const string guildId = "920000000000000001";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: false));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/audit-log");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAuditLog_WhenAdmin_Returns200WithEntriesNewestFirst()
    {
        const string id      = "520000000000000002";
        const string guildId = "920000000000000002";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id, name: "Bhahlou"));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            db.GuildAuditLogs.Add(new GuildAuditLog
            {
                GuildId = guildId, ActorDiscordId = id, ActionType = GuildAuditAction.GuildRegistered,
                OccurredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            });
            db.GuildAuditLogs.Add(new GuildAuditLog
            {
                GuildId = guildId, ActorDiscordId = id, ActionType = GuildAuditAction.SettingsUpdated,
                OccurredAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            });
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/audit-log");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var entries = json.GetProperty("entries");
        entries.GetArrayLength().Should().Be(2);
        entries[0].GetProperty("actionType").GetString().Should().Be("SettingsUpdated");
        entries[0].GetProperty("actorUsername").GetString().Should().Be("Bhahlou");
        json.GetProperty("hasMore").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task GetAuditLog_WithPageSize_RespectsHasMoreAndPagination()
    {
        const string id      = "520000000000000003";
        const string guildId = "920000000000000003";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            for (var i = 0; i < 3; i++)
            {
                db.GuildAuditLogs.Add(new GuildAuditLog
                {
                    GuildId = guildId, ActorDiscordId = id, ActionType = GuildAuditAction.GuildRegistered,
                    OccurredAt = new DateTime(2026, 1, 1 + i, 0, 0, 0, DateTimeKind.Utc),
                });
            }
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/audit-log?page=1&pageSize=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("entries").GetArrayLength().Should().Be(2);
        json.GetProperty("hasMore").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task GetAuditLog_WithActionTypeFilter_OnlyReturnsMatchingEntries()
    {
        const string id      = "520000000000000004";
        const string guildId = "920000000000000004";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            db.GuildAuditLogs.Add(new GuildAuditLog
            {
                GuildId = guildId, ActorDiscordId = id, ActionType = GuildAuditAction.GuildRegistered,
                OccurredAt = DateTime.UtcNow,
            });
            db.GuildAuditLogs.Add(new GuildAuditLog
            {
                GuildId = guildId, ActorDiscordId = id, ActionType = GuildAuditAction.SettingsUpdated,
                OccurredAt = DateTime.UtcNow,
            });
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/audit-log?actionType=SettingsUpdated");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var entries = json.GetProperty("entries");
        entries.GetArrayLength().Should().Be(1);
        entries[0].GetProperty("actionType").GetString().Should().Be("SettingsUpdated");
    }

    [Fact]
    public async Task GetAuditLog_WithCategoryFilter_OnlyReturnsEntriesInThatCategory()
    {
        const string id      = "520000000000000005";
        const string guildId = "920000000000000005";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            db.GuildAuditLogs.Add(new GuildAuditLog
            {
                GuildId = guildId, ActorDiscordId = id, ActionType = GuildAuditAction.SettingsUpdated,
                OccurredAt = DateTime.UtcNow,
            });
            db.GuildAuditLogs.Add(new GuildAuditLog
            {
                GuildId = guildId, ActorDiscordId = id, ActionType = GuildAuditAction.MemberJoined,
                OccurredAt = DateTime.UtcNow,
            });
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/audit-log?category=Roster");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var entries = json.GetProperty("entries");
        entries.GetArrayLength().Should().Be(1);
        entries[0].GetProperty("actionType").GetString().Should().Be("MemberJoined");
        entries[0].GetProperty("category").GetString().Should().Be("Roster");
    }
}
