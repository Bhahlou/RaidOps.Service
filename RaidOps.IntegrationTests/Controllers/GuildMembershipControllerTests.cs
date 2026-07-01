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
            // characterClassId lets the audit log viewer show the class icon/color next to the name.
            log.Details.Should().Contain("\"characterClassId\":\"8\"");
        }
    }

    // ── GetEligibleGuildsBulk ───────────────────────────────────────────────

    [Fact]
    public async Task GetEligibleGuildsBulk_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/characters/eligible-guilds");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetEligibleGuildsBulk_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();
        var response = await client.GetAsync("/api/v1/characters/eligible-guilds");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetEligibleGuildsBulk_WithNoActiveCharacters_ReturnsEmptyList()
    {
        const string id = "400000000000000090";
        await SeedAsync(db => { db.Users.Add(TestDataBuilder.CreateUser(id)); return Task.CompletedTask; });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync("/api/v1/characters/eligible-guilds");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<GuildEligibilityResponse>>();
        body.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEligibleGuildsBulk_WithOpenGuildAndMultipleChars_ReturnsGuildWithAllEligibleChars()
    {
        const string id      = "400000000000000091";
        const string guildId = "820000000000000091";
        var char1Id = await SeedUserWithCharacter(id, bnetCharacterId: 80091, name: "Bhahlou");
        var char2Id = await SeedUserWithCharacter2ndChar(id, bnetCharacterId: 80092, name: "Bhahlheal");
        await SeedAsync(db =>
        {
            db.Guilds.Add(new Guild { Id = guildId, Name = "Iron Council", IsRegistered = true, RosterMode = RosterMode.Open });
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync("/api/v1/characters/eligible-guilds");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<GuildEligibilityResponse>>(ApiJsonOptions);
        body.Should().ContainSingle(g => g.GuildId == guildId);
        body![0].EligibleCharacters.Should().HaveCount(2);
        body[0].EligibleCharacters.Should().Contain(c => c.Id == char1Id);
        body[0].EligibleCharacters.Should().Contain(c => c.Id == char2Id);
    }

    [Fact]
    public async Task GetEligibleGuildsBulk_WhenOneCharAlreadyMember_ExcludesThemFromGuild()
    {
        const string id      = "400000000000000092";
        const string guildId = "820000000000000092";
        var char1Id = await SeedUserWithCharacter(id, bnetCharacterId: 80093, name: "MainChar");
        var char2Id = await SeedUserWithCharacter2ndChar(id, bnetCharacterId: 80094, name: "AltChar");
        await SeedAsync(db =>
        {
            db.Guilds.Add(new Guild { Id = guildId, Name = "Open Guild", IsRegistered = true, RosterMode = RosterMode.Open });
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId));
            db.GuildMemberships.Add(new GuildMembership
            {
                CharacterId = char1Id, GuildId = guildId,
                CharacterRank = CharacterRank.Main, JoinedAt = DateTime.UtcNow,
            });
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync("/api/v1/characters/eligible-guilds");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<GuildEligibilityResponse>>(ApiJsonOptions);
        body.Should().ContainSingle(g => g.GuildId == guildId);
        body![0].EligibleCharacters.Should().ContainSingle(c => c.Id == char2Id);
        body[0].EligibleCharacters.Should().NotContain(c => c.Id == char1Id);
    }

    [Fact]
    public async Task GetEligibleGuildsBulk_WhenAllCharsAlreadyMembers_GuildExcluded()
    {
        const string id      = "400000000000000093";
        const string guildId = "820000000000000093";
        var charId = await SeedUserWithCharacter(id, bnetCharacterId: 80095, name: "OnlyChar");
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

        var response = await client.GetAsync("/api/v1/characters/eligible-guilds");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<GuildEligibilityResponse>>(ApiJsonOptions);
        body.Should().BeEmpty();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds a user with an active character on an isolated realm slug.
    /// Uses the <paramref name="bnetCharacterId"/> to guarantee unique realm slugs across tests.
    /// </summary>
    private async Task<int> SeedUserWithCharacter(string discordId, long bnetCharacterId = 80001, string name = "TestMage")
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaidOpsDbContext>();

        db.Users.Add(TestDataBuilder.CreateUser(discordId));
        await db.SaveChangesAsync();

        var realm = TestDataBuilder.CreateRealm(slug: $"realm-mb-{bnetCharacterId}");
        db.Realms.Add(realm);
        await db.SaveChangesAsync();

        var character = TestDataBuilder.CreateCharacter(discordId, realm.Id, isActive: true, bnetCharacterId: bnetCharacterId, name: name);
        db.Characters.Add(character);
        await db.SaveChangesAsync();

        return character.Id;
    }

    /// <summary>
    /// Seeds a second active character for an existing user (skips user creation to avoid PK conflict).
    /// </summary>
    private async Task<int> SeedUserWithCharacter2ndChar(string discordId, long bnetCharacterId, string name = "TestAlt")
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaidOpsDbContext>();

        var realm = TestDataBuilder.CreateRealm(slug: $"realm-mb-{bnetCharacterId}");
        db.Realms.Add(realm);
        await db.SaveChangesAsync();

        var character = TestDataBuilder.CreateCharacter(discordId, realm.Id, isActive: true, bnetCharacterId: bnetCharacterId, name: name);
        db.Characters.Add(character);
        await db.SaveChangesAsync();

        return character.Id;
    }
}
