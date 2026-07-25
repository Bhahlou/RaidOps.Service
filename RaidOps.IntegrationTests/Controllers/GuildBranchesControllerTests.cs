using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RaidOps.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for <see cref="RaidOps.API.Controllers.v1.GuildBranchesController"/>.
/// Uses a shared Testcontainers PostgreSQL instance via <see cref="RaidOpsWebApplicationFactory"/>.
/// All Discord IDs are in the 700… range and guild IDs in the 970… range to avoid primary-key
/// conflicts with other test classes.
/// </summary>
[Collection("Integration")]
public class GuildBranchesControllerTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    // ── Auth enforcement ────────────────────────────────────────────────────

    [Fact]
    public async Task GetBranches_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/guilds/970000000000000001/branches");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ActivateBranch_WithoutToken_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/guilds/970000000000000001/branches", new { branchId = 1 });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeactivateBranch_WithoutToken_Returns401()
    {
        var response = await Client.DeleteAsync("/api/v1/guilds/970000000000000001/branches/1");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateRosterSettings_WithoutToken_Returns401()
    {
        var body = JsonContent.Create(new { rosterMode = "Open" });
        var response = await Client.PatchAsync("/api/v1/guilds/970000000000000001/branches/1/roster-settings", body);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GetBranches ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetBranches_WhenUserNotAdmin_Returns400()
    {
        const string id      = "700000000000000001";
        const string guildId = "970000000000000001";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: false));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/branches");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetBranches_WhenAdmin_ReturnsSeededBranch()
    {
        const string id      = "700000000000000002";
        const string guildId = "970000000000000002";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.GuildBranches.Add(TestDataBuilder.CreateGuildBranch(guildId, officerRoleIds: ["role-1"]));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/branches");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetArrayLength().Should().Be(1);
        json[0].GetProperty("branchId").GetInt32().Should().Be(1);
        json[0].GetProperty("isActive").GetBoolean().Should().BeTrue();
    }

    // ── ActivateBranch ─────────────────────────────────────────────────────

    [Fact]
    public async Task ActivateBranch_WhenUserNotAdmin_Returns400()
    {
        const string id      = "700000000000000003";
        const string guildId = "970000000000000003";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: false));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PostAsJsonAsync($"/api/v1/guilds/{guildId}/branches", new { branchId = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ActivateBranch_WhenAdmin_Returns200AndCreatesBranch()
    {
        const string id      = "700000000000000004";
        const string guildId = "970000000000000004";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PostAsJsonAsync($"/api/v1/guilds/{guildId}/branches", new { branchId = 2 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var branch = await db.GuildBranches.FirstOrDefaultAsync(b => b.GuildId == guildId && b.BranchId == 2);
            branch.Should().NotBeNull();
            branch!.IsActive.Should().BeTrue();
        }
    }

    [Fact]
    public async Task ActivateBranch_AlreadyActive_Returns400()
    {
        const string id      = "700000000000000005";
        const string guildId = "970000000000000005";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.GuildBranches.Add(TestDataBuilder.CreateGuildBranch(guildId));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PostAsJsonAsync($"/api/v1/guilds/{guildId}/branches", new { branchId = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── DeactivateBranch ───────────────────────────────────────────────────

    [Fact]
    public async Task DeactivateBranch_WhenAdmin_Returns200AndDeactivates()
    {
        const string id      = "700000000000000006";
        const string guildId = "970000000000000006";
        var branch = TestDataBuilder.CreateGuildBranch(guildId);
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.GuildBranches.Add(branch);
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.DeleteAsync($"/api/v1/guilds/{guildId}/branches/{branch.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var updated = await db.GuildBranches.FindAsync(branch.Id);
            updated!.IsActive.Should().BeFalse();
        }
    }

    [Fact]
    public async Task DeactivateBranch_WhenBranchNotFound_Returns400()
    {
        const string id      = "700000000000000007";
        const string guildId = "970000000000000007";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.DeleteAsync($"/api/v1/guilds/{guildId}/branches/999999");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── UpdateRosterSettings ───────────────────────────────────────────────

    [Fact]
    public async Task UpdateRosterSettings_WhenOfficerOfBranch_Returns200AndPersists()
    {
        const string id      = "700000000000000008";
        const string guildId = "970000000000000008";
        var branch = TestDataBuilder.CreateGuildBranch(guildId, officerRoleIds: []);
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.GuildBranches.Add(branch);
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);
        var body = JsonContent.Create(new { rosterMode = "DiscordRoleOnly", rosterRoleIds = new[] { "role-1" }, officerRoleIds = new[] { "role-2" } });

        var response = await client.PatchAsync($"/api/v1/guilds/{guildId}/branches/{branch.Id}/roster-settings", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var updated = await db.GuildBranches.FindAsync(branch.Id);
            updated!.RosterMode.Should().Be(RosterMode.DiscordRoleOnly);
            updated.RosterRoleIds.Should().Equal("role-1");
            updated.OfficerRoleIds.Should().Equal("role-2");
        }
    }

    [Fact]
    public async Task UpdateRosterSettings_WhenNotOfficerOfBranch_Returns400()
    {
        const string id      = "700000000000000009";
        const string guildId = "970000000000000009";
        var branch = TestDataBuilder.CreateGuildBranch(guildId, officerRoleIds: []);
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.GuildBranches.Add(branch);
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: false));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);
        var body = JsonContent.Create(new { rosterMode = "Open" });

        var response = await client.PatchAsync($"/api/v1/guilds/{guildId}/branches/{branch.Id}/roster-settings", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateRosterSettings_OnSuccess_WritesAuditLog()
    {
        const string id      = "700000000000000010";
        const string guildId = "970000000000000010";
        var branch = TestDataBuilder.CreateGuildBranch(guildId, officerRoleIds: []);
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.GuildBranches.Add(branch);
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);
        var body = JsonContent.Create(new { rosterMode = "DiscordRoleOnly", rosterRoleIds = new[] { "role-1" } });

        await client.PatchAsync($"/api/v1/guilds/{guildId}/branches/{branch.Id}/roster-settings", body);

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var log = await db.GuildAuditLogs.FirstOrDefaultAsync(l =>
                l.GuildId == guildId && l.ActionType == GuildAuditAction.BranchRosterSettingsUpdated);
            log.Should().NotBeNull();
            log!.ActorDiscordId.Should().Be(id);
        }
    }
}
