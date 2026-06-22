using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Enums;
using RaidOps.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RaidOps.IntegrationTests.Controllers;

[Collection("Integration")]
public class GuildSettingsControllerTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    // ── Auth enforcement ────────────────────────────────────────────────────

    [Fact]
    public async Task GetSettings_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/guilds/123456789012345678/settings");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetDiscordRoles_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/guilds/123456789012345678/discord-roles");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateSettings_WithoutToken_Returns401()
    {
        var body = JsonContent.Create(new { timezone = "UTC", rosterMode = "Open" });

        var response = await Client.PatchAsync("/api/v1/guilds/123456789012345678/settings", body);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Business logic ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetSettings_GuildNotRegistered_Returns400()
    {
        const string id      = "510000000000000001";
        const string guildId = "910000000000000001";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: false));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/settings");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetSettings_WhenRegistered_Returns200WithSettings()
    {
        const string id      = "510000000000000002";
        const string guildId = "910000000000000002";
        await SeedAsync(db =>
        {
            var guild = TestDataBuilder.CreateGuild(guildId, isRegistered: true);
            guild.Timezone = "Europe/Paris";
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(guild);
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/settings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("timezone").GetString().Should().Be("Europe/Paris");
    }

    [Fact]
    public async Task UpdateSettings_WhenUserNotAdmin_Returns400()
    {
        const string id      = "510000000000000003";
        const string guildId = "910000000000000003";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: false));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);
        var body = JsonContent.Create(new { timezone = "UTC", rosterMode = "Open" });

        var response = await client.PatchAsync($"/api/v1/guilds/{guildId}/settings", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateSettings_WhenRegisteredAndAdmin_Returns200()
    {
        const string id      = "510000000000000004";
        const string guildId = "910000000000000004";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);
        var body = JsonContent.Create(new { timezone = "Europe/Paris", rosterMode = "Open" });

        var response = await client.PatchAsync($"/api/v1/guilds/{guildId}/settings", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetDiscordRoles_WhenUserNotAdmin_Returns400()
    {
        const string id      = "510000000000000005";
        const string guildId = "910000000000000005";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: false));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/discord-roles");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetDiscordRoles_WhenAdmin_Returns200WithEmptyList()
    {
        const string id      = "510000000000000006";
        const string guildId = "910000000000000006";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/discord-roles");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.ValueKind.Should().Be(JsonValueKind.Array);
    }

    // ── Audit log persistence ─────────────────────────────────────────────

    [Fact]
    public async Task UpdateSettings_TimezoneChanged_WritesAuditLogWithOldAndNewValues()
    {
        const string id      = "510000000000000007";
        const string guildId = "910000000000000007";
        await SeedAsync(db =>
        {
            var guild = TestDataBuilder.CreateGuild(guildId, isRegistered: true);
            guild.Timezone = "UTC";
            guild.RosterMode = RosterMode.Open;
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(guild);
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);
        var body = JsonContent.Create(new { timezone = "Europe/Paris", rosterMode = "Open" });

        var response = await client.PatchAsync($"/api/v1/guilds/{guildId}/settings", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var log = await db.GuildAuditLogs.FirstOrDefaultAsync(l =>
                l.GuildId == guildId && l.ActionType == GuildAuditAction.SettingsUpdated);
            log.Should().NotBeNull();
            log!.ActorDiscordId.Should().Be(id);
            log.Details.Should().Contain("\"changedFields\":\"timezone\"");
            log.Details.Should().Contain("\"oldTimezone\":\"UTC\"");
            log.Details.Should().Contain("\"newTimezone\":\"Europe/Paris\"");
        }
    }

    [Fact]
    public async Task UpdateSettings_NothingChanged_DoesNotWriteAuditLog()
    {
        const string id      = "510000000000000008";
        const string guildId = "910000000000000008";
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
        var body = JsonContent.Create(new { timezone = "Europe/Paris", rosterMode = "Open" });

        var response = await client.PatchAsync($"/api/v1/guilds/{guildId}/settings", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var log = await db.GuildAuditLogs.FirstOrDefaultAsync(l =>
                l.GuildId == guildId && l.ActionType == GuildAuditAction.SettingsUpdated);
            log.Should().BeNull();
        }
    }

    [Fact]
    public async Task UpdateSettings_SwitchToDiscordRoleOnly_LogsChangedFieldsWithoutCrashingWhenBotHasNoRoles()
    {
        // NoOpGuildService (the integration test bot stub) always returns an empty role list,
        // so the role threshold's name/color/icon can never resolve here — this guards that the
        // settings update still succeeds and logs the field change rather than failing/crashing.
        const string id      = "510000000000000009";
        const string guildId = "910000000000000009";
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
        var body = JsonContent.Create(new
        {
            timezone = "Europe/Paris", rosterMode = "DiscordRoleOnly", minRosterRoleId = "300000000000000009",
        });

        var response = await client.PatchAsync($"/api/v1/guilds/{guildId}/settings", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var log = await db.GuildAuditLogs.FirstOrDefaultAsync(l =>
                l.GuildId == guildId && l.ActionType == GuildAuditAction.SettingsUpdated);
            log.Should().NotBeNull();
            log!.Details.Should().Contain("\"changedFields\":\"rosterMode,minRosterRoleId\"");
            log.Details.Should().NotContain("newMinRosterRoleName");
        }
    }

    [Fact]
    public async Task UpdateSettings_SwitchFromDiscordRoleOnlyToOpen_DoesNotLogMinRosterRoleId()
    {
        const string id      = "510000000000000010";
        const string guildId = "910000000000000010";
        await SeedAsync(db =>
        {
            var guild = TestDataBuilder.CreateGuild(guildId, isRegistered: true);
            guild.Timezone = "Europe/Paris";
            guild.RosterMode = RosterMode.DiscordRoleOnly;
            guild.MinRosterRoleId = "300000000000000010";
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(guild);
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);
        var body = JsonContent.Create(new { timezone = "Europe/Paris", rosterMode = "Open" });

        var response = await client.PatchAsync($"/api/v1/guilds/{guildId}/settings", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var log = await db.GuildAuditLogs.FirstOrDefaultAsync(l =>
                l.GuildId == guildId && l.ActionType == GuildAuditAction.SettingsUpdated);
            log.Should().NotBeNull();
            log!.Details.Should().Contain("\"changedFields\":\"rosterMode\"");
            log.Details.Should().NotContain("MinRosterRole");
        }
    }
}
