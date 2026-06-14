using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaidOps.Application.Contracts.Guilds.Memberships.Responses;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Discord;
using RaidOps.Infrastructure.Persistence.Implementations;
using RaidOps.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RaidOps.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for <see cref="RaidOps.API.Controllers.v1.GuildMembershipController"/>.
/// Uses a shared Testcontainers PostgreSQL instance via <see cref="RaidOpsWebApplicationFactory"/>.
/// All Discord IDs are in the 400… range and guild IDs in the 820… range to avoid primary-key
/// conflicts with other test classes.
/// </summary>
[Collection("Integration")]
public class GuildMembershipControllerTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    // ── Auth enforcement ────────────────────────────────────────────────────

    [Fact]
    public async Task GetCharacterMemberships_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/characters/1/memberships");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetEligibleGuilds_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/characters/1/eligible-guilds");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task JoinGuild_WithoutToken_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/characters/1/memberships/guild-1",
            new { characterRank = 1 });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateCharacterRank_WithoutToken_Returns401()
    {
        var body = JsonContent.Create(new { characterRank = 2 });
        var response = await Client.PatchAsync("/api/v1/characters/1/memberships/guild-1", body);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LeaveGuild_WithoutToken_Returns401()
    {
        var response = await Client.DeleteAsync("/api/v1/characters/1/memberships/guild-1");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMyCharactersInGuild_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/guilds/guild-1/my-characters");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GetCharacterMemberships ─────────────────────────────────────────────

    [Fact]
    public async Task GetCharacterMemberships_WhenCharacterNotFound_Returns400()
    {
        const string id = "400000000000000001";
        await SeedAsync(db => { db.Users.Add(TestDataBuilder.CreateUser(id)); return Task.CompletedTask; });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync("/api/v1/characters/99999/memberships");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetCharacterMemberships_WhenNotOwner_Returns400()
    {
        const string ownerId     = "400000000000000011";
        const string requesterId = "400000000000000012";
        var charId = await SeedUserWithCharacter(ownerId, bnetCharacterId: 80011);
        await SeedAsync(db => { db.Users.Add(TestDataBuilder.CreateUser(requesterId)); return Task.CompletedTask; });
        var client = CreateAuthenticatedClient(discordId: requesterId);

        var response = await client.GetAsync($"/api/v1/characters/{charId}/memberships");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetCharacterMemberships_WhenNoMemberships_ReturnsEmptyList()
    {
        const string id = "400000000000000020";
        var charId = await SeedUserWithCharacter(id, bnetCharacterId: 80020);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/characters/{charId}/memberships");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<GuildMembershipResponse>>();
        body.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCharacterMemberships_WhenMembershipsExist_ReturnsList()
    {
        const string id      = "400000000000000021";
        const string guildId = "820000000000000021";
        var charId = await SeedUserWithCharacter(id, bnetCharacterId: 80021);
        await SeedAsync(db =>
        {
            db.Guilds.Add(new Guild { Id = guildId, Name = "Test Guild", IsRegistered = true });
            db.GuildMemberships.Add(new GuildMembership
            {
                CharacterId = charId, GuildId = guildId,
                CharacterRank = CharacterRank.Main, JoinedAt = DateTime.UtcNow,
            });
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/characters/{charId}/memberships");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<GuildMembershipResponse>>(ApiJsonOptions);
        body.Should().ContainSingle(m => m.GuildId == guildId && m.CharacterRank == CharacterRank.Main);
    }

    // ── GetEligibleGuilds ───────────────────────────────────────────────────

    [Fact]
    public async Task GetEligibleGuilds_WhenCharacterNotFound_Returns400()
    {
        const string id = "400000000000000030";
        await SeedAsync(db => { db.Users.Add(TestDataBuilder.CreateUser(id)); return Task.CompletedTask; });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync("/api/v1/characters/99999/eligible-guilds");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetEligibleGuilds_WithOpenModeGuildAndDiscordMembership_ReturnsEligibleGuild()
    {
        const string id      = "400000000000000031";
        const string guildId = "820000000000000031";
        var charId = await SeedUserWithCharacter(id, bnetCharacterId: 80031);
        await SeedAsync(db =>
        {
            db.Guilds.Add(new Guild { Id = guildId, Name = "Open Guild", IsRegistered = true, RosterMode = RosterMode.Open });
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/characters/{charId}/eligible-guilds");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<EligibleGuildResponse>>();
        body.Should().ContainSingle(g => g.GuildId == guildId);
    }

    [Fact]
    public async Task GetEligibleGuilds_WhenAlreadyMember_ExcludesGuild()
    {
        const string id      = "400000000000000032";
        const string guildId = "820000000000000032";
        var charId = await SeedUserWithCharacter(id, bnetCharacterId: 80032);
        await SeedAsync(db =>
        {
            db.Guilds.Add(new Guild { Id = guildId, Name = "Open Guild", IsRegistered = true, RosterMode = RosterMode.Open });
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId));
            db.GuildMemberships.Add(new GuildMembership
            {
                CharacterId = charId, GuildId = guildId,
                CharacterRank = CharacterRank.Main, JoinedAt = DateTime.UtcNow,
            });
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/characters/{charId}/eligible-guilds");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<EligibleGuildResponse>>();
        body.Should().BeEmpty();
    }

    // ── JoinGuild ──────────────────────────────────────────────────────────

    [Fact]
    public async Task JoinGuild_WhenCharacterNotFound_Returns400()
    {
        const string id = "400000000000000040";
        await SeedAsync(db => { db.Users.Add(TestDataBuilder.CreateUser(id)); return Task.CompletedTask; });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PostAsJsonAsync("/api/v1/characters/99999/memberships/guild-x",
            new { characterRank = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task JoinGuild_WhenGuildNotFound_Returns400()
    {
        const string id = "400000000000000041";
        var charId = await SeedUserWithCharacter(id, bnetCharacterId: 80041);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/characters/{charId}/memberships/nonexistent-guild-000000041",
            new { characterRank = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task JoinGuild_WhenGuildNotRegistered_Returns400()
    {
        const string id      = "400000000000000042";
        const string guildId = "820000000000000042";
        var charId = await SeedUserWithCharacter(id, bnetCharacterId: 80042);
        await SeedAsync(db =>
        {
            db.Guilds.Add(new Guild { Id = guildId, Name = "Unregistered Guild", IsRegistered = false });
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/characters/{charId}/memberships/{guildId}",
            new { characterRank = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task JoinGuild_WhenAlreadyMember_Returns400()
    {
        const string id      = "400000000000000043";
        const string guildId = "820000000000000043";
        var charId = await SeedUserWithCharacter(id, bnetCharacterId: 80043);
        await SeedAsync(db =>
        {
            db.Guilds.Add(new Guild { Id = guildId, Name = "Open Guild", IsRegistered = true, RosterMode = RosterMode.Open });
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId));
            db.GuildMemberships.Add(new GuildMembership
            {
                CharacterId = charId, GuildId = guildId,
                CharacterRank = CharacterRank.Main, JoinedAt = DateTime.UtcNow,
            });
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/characters/{charId}/memberships/{guildId}",
            new { characterRank = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task JoinGuild_WithOpenModeAndDiscordMembership_Returns200AndCreatesMembership()
    {
        const string id      = "400000000000000044";
        const string guildId = "820000000000000044";
        var charId = await SeedUserWithCharacter(id, bnetCharacterId: 80044);
        await SeedAsync(db =>
        {
            db.Guilds.Add(new Guild { Id = guildId, Name = "Open Guild", IsRegistered = true, RosterMode = RosterMode.Open });
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/characters/{charId}/memberships/{guildId}",
            new { characterRank = (int)CharacterRank.Main });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var membership = await db.GuildMemberships.FindAsync(charId, guildId);
            membership.Should().NotBeNull();
            membership!.CharacterRank.Should().Be(CharacterRank.Main);
        }
    }

    // ── UpdateCharacterRank ────────────────────────────────────────────────

    [Fact]
    public async Task UpdateCharacterRank_WhenNotAMember_Returns400()
    {
        const string id      = "400000000000000050";
        const string guildId = "820000000000000050";
        var charId = await SeedUserWithCharacter(id, bnetCharacterId: 80050);
        await SeedAsync(db =>
        {
            db.Guilds.Add(new Guild { Id = guildId, Name = "Test Guild", IsRegistered = true });
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var body = JsonContent.Create(new { characterRank = (int)CharacterRank.Alt });
        var response = await client.PatchAsync($"/api/v1/characters/{charId}/memberships/{guildId}", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateCharacterRank_WhenMember_Returns200AndUpdatesRank()
    {
        const string id      = "400000000000000051";
        const string guildId = "820000000000000051";
        var charId = await SeedUserWithCharacter(id, bnetCharacterId: 80051);
        await SeedAsync(db =>
        {
            db.Guilds.Add(new Guild { Id = guildId, Name = "Test Guild", IsRegistered = true });
            db.GuildMemberships.Add(new GuildMembership
            {
                CharacterId = charId, GuildId = guildId,
                CharacterRank = CharacterRank.Main, JoinedAt = DateTime.UtcNow,
            });
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var body = JsonContent.Create(new { characterRank = (int)CharacterRank.Alt });
        var response = await client.PatchAsync($"/api/v1/characters/{charId}/memberships/{guildId}", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var membership = await db.GuildMemberships.FindAsync(charId, guildId);
            membership!.CharacterRank.Should().Be(CharacterRank.Alt);
        }
    }

    // ── LeaveGuild ────────────────────────────────────────────────────────

    [Fact]
    public async Task LeaveGuild_WhenNotAMember_Returns400()
    {
        const string id      = "400000000000000060";
        const string guildId = "820000000000000060";
        var charId = await SeedUserWithCharacter(id, bnetCharacterId: 80060);
        await SeedAsync(db =>
        {
            db.Guilds.Add(new Guild { Id = guildId, Name = "Test Guild", IsRegistered = true });
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.DeleteAsync($"/api/v1/characters/{charId}/memberships/{guildId}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LeaveGuild_WhenMember_Returns200AndRemovesMembership()
    {
        const string id      = "400000000000000061";
        const string guildId = "820000000000000061";
        var charId = await SeedUserWithCharacter(id, bnetCharacterId: 80061);
        await SeedAsync(db =>
        {
            db.Guilds.Add(new Guild { Id = guildId, Name = "Test Guild", IsRegistered = true });
            db.GuildMemberships.Add(new GuildMembership
            {
                CharacterId = charId, GuildId = guildId,
                CharacterRank = CharacterRank.Main, JoinedAt = DateTime.UtcNow,
            });
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.DeleteAsync($"/api/v1/characters/{charId}/memberships/{guildId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var membership = await db.GuildMemberships.FindAsync(charId, guildId);
            membership.Should().BeNull();
        }
    }

    // ── GetMyCharactersInGuild ─────────────────────────────────────────────

    [Fact]
    public async Task GetMyCharactersInGuild_WhenNoCharactersOnRoster_ReturnsEmptyList()
    {
        const string id      = "400000000000000070";
        const string guildId = "820000000000000070";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(new Guild { Id = guildId, Name = "Test Guild", IsRegistered = true });
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/my-characters");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<CharacterInGuildResponse>>();
        body.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMyCharactersInGuild_WhenCharactersOnRoster_ReturnsList()
    {
        const string id      = "400000000000000071";
        const string guildId = "820000000000000071";
        var charId = await SeedUserWithCharacter(id, bnetCharacterId: 80071);
        await SeedAsync(db =>
        {
            db.Guilds.Add(new Guild { Id = guildId, Name = "Test Guild", IsRegistered = true });
            db.GuildMemberships.Add(new GuildMembership
            {
                CharacterId = charId, GuildId = guildId,
                CharacterRank = CharacterRank.Alt, JoinedAt = DateTime.UtcNow,
            });
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/my-characters");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<CharacterInGuildResponse>>(ApiJsonOptions);
        body.Should().ContainSingle(c => c.CharacterId == charId && c.CharacterRank == CharacterRank.Alt);
    }

    // ── Audit log persistence ─────────────────────────────────────────────

    [Fact]
    public async Task JoinGuild_OnSuccess_WritesAuditLogEntry()
    {
        const string id      = "400000000000000080";
        const string guildId = "820000000000000080";
        var charId = await SeedUserWithCharacter(id, bnetCharacterId: 80080);
        await SeedAsync(db =>
        {
            db.Guilds.Add(new Guild { Id = guildId, Name = "Open Guild", IsRegistered = true, RosterMode = RosterMode.Open });
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        await client.PostAsJsonAsync(
            $"/api/v1/characters/{charId}/memberships/{guildId}",
            new { characterRank = (int)CharacterRank.Main });

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var log = await db.GuildAuditLogs.FirstOrDefaultAsync(l =>
                l.GuildId == guildId && l.ActionType == GuildAuditAction.MemberJoined);
            log.Should().NotBeNull();
            log!.ActorDiscordId.Should().Be(id);
        }
    }

    [Fact]
    public async Task LeaveGuild_OnSuccess_WritesAuditLogEntry()
    {
        const string id      = "400000000000000081";
        const string guildId = "820000000000000081";
        var charId = await SeedUserWithCharacter(id, bnetCharacterId: 80081);
        await SeedAsync(db =>
        {
            db.Guilds.Add(new Guild { Id = guildId, Name = "Test Guild", IsRegistered = true });
            db.GuildMemberships.Add(new GuildMembership
            {
                CharacterId = charId, GuildId = guildId,
                CharacterRank = CharacterRank.Main, JoinedAt = DateTime.UtcNow,
            });
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        await client.DeleteAsync($"/api/v1/characters/{charId}/memberships/{guildId}");

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var log = await db.GuildAuditLogs.FirstOrDefaultAsync(l =>
                l.GuildId == guildId && l.ActionType == GuildAuditAction.MemberLeft);
            log.Should().NotBeNull();
            log!.ActorDiscordId.Should().Be(id);
        }
    }

    [Fact]
    public async Task UpdateCharacterRank_OnSuccess_WritesAuditLogEntry()
    {
        const string id      = "400000000000000082";
        const string guildId = "820000000000000082";
        var charId = await SeedUserWithCharacter(id, bnetCharacterId: 80082);
        await SeedAsync(db =>
        {
            db.Guilds.Add(new Guild { Id = guildId, Name = "Test Guild", IsRegistered = true });
            db.GuildMemberships.Add(new GuildMembership
            {
                CharacterId = charId, GuildId = guildId,
                CharacterRank = CharacterRank.Main, JoinedAt = DateTime.UtcNow,
            });
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var body = JsonContent.Create(new { characterRank = (int)CharacterRank.Alt });
        await client.PatchAsync($"/api/v1/characters/{charId}/memberships/{guildId}", body);

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var log = await db.GuildAuditLogs.FirstOrDefaultAsync(l =>
                l.GuildId == guildId && l.ActionType == GuildAuditAction.MemberRankUpdated);
            log.Should().NotBeNull();
            log!.ActorDiscordId.Should().Be(id);
            log.Details.Should().Contain("Alt");
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds a user with an active character on an isolated realm slug.
    /// Uses the <paramref name="bnetCharacterId"/> to guarantee unique realm slugs across tests.
    /// </summary>
    private async Task<int> SeedUserWithCharacter(string discordId, long bnetCharacterId = 80001)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaidOpsDbContext>();

        db.Users.Add(TestDataBuilder.CreateUser(discordId));
        await db.SaveChangesAsync();

        var realm = TestDataBuilder.CreateRealm(slug: $"realm-mb-{bnetCharacterId}");
        db.Realms.Add(realm);
        await db.SaveChangesAsync();

        var character = TestDataBuilder.CreateCharacter(discordId, realm.Id, isActive: true, bnetCharacterId: bnetCharacterId);
        db.Characters.Add(character);
        await db.SaveChangesAsync();

        return character.Id;
    }
}
