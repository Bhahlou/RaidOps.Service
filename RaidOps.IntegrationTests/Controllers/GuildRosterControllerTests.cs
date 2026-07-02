using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RaidOps.Application.Contracts.Guilds.Roster.Responses;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Infrastructure.Persistence.Implementations;
using RaidOps.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;

namespace RaidOps.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for <see cref="RaidOps.API.Controllers.v1.GuildRosterController"/>.
/// Uses a shared Testcontainers PostgreSQL instance via <see cref="RaidOpsWebApplicationFactory"/>.
/// All Discord IDs are in the 600… range and guild IDs in the 940… range to avoid primary-key
/// conflicts with other test classes.
/// </summary>
[Collection("Integration")]
public class GuildRosterControllerTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    // ── Auth enforcement ────────────────────────────────────────────────────

    [Fact]
    public async Task GetRoster_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/guilds/940000000000000001/roster");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Business logic ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetRoster_WhenGuildNotRegistered_Returns400()
    {
        const string id      = "600000000000000001";
        const string guildId = "940000000000000001";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: false));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/roster");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetRoster_WhenRequesterNotDiscordMember_Returns400()
    {
        const string id      = "600000000000000002";
        const string guildId = "940000000000000002";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/roster");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetRoster_WhenRosterModeClosedWithoutRequiredRole_Returns400()
    {
        const string id      = "600000000000000003";
        const string guildId = "940000000000000003";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(new Guild
            {
                Id = guildId, Name = "Closed Guild", IsRegistered = true,
                RosterMode = RosterMode.DiscordRoleOnly, MinRosterRoleId = "role-1",
            });
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/roster");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetRoster_WhenRosterAccessGranted_Returns200WithMembersOrderedByRankThenName()
    {
        const string id      = "600000000000000004";
        const string guildId = "940000000000000004";
        var mainZed = await SeedActiveCharacter(id, bnetCharacterId: 90004001, name: "Zed");
        var mainBob = await SeedActiveCharacter(id, bnetCharacterId: 90004002, name: "Bob");
        var altAaron = await SeedActiveCharacter(id, bnetCharacterId: 90004003, name: "Aaron");
        await SeedAsync(db =>
        {
            db.Guilds.Add(new Guild { Id = guildId, Name = "Open Guild", IsRegistered = true, RosterMode = RosterMode.Open });
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId));
            db.GuildMemberships.Add(new GuildMembership { CharacterId = mainZed, GuildId = guildId, CharacterRank = CharacterRank.Main, JoinedAt = DateTime.UtcNow });
            db.GuildMemberships.Add(new GuildMembership { CharacterId = mainBob, GuildId = guildId, CharacterRank = CharacterRank.Main, JoinedAt = DateTime.UtcNow });
            db.GuildMemberships.Add(new GuildMembership { CharacterId = altAaron, GuildId = guildId, CharacterRank = CharacterRank.Alt, JoinedAt = DateTime.UtcNow });
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/roster");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<GuildRosterMemberResponse>>(ApiJsonOptions);
        body!.Select(m => m.CharacterName).Should().Equal("Bob", "Zed", "Aaron");
        body[0].ClassId.Should().Be(8);
        body[0].RealmName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetRoster_ExcludesInactiveCharacters()
    {
        const string id      = "600000000000000005";
        const string guildId = "940000000000000005";
        var activeChar = await SeedActiveCharacter(id, bnetCharacterId: 90005001, name: "Active");
        var inactiveChar = await SeedActiveCharacter(id, bnetCharacterId: 90005002, name: "Inactive", isActive: false);
        await SeedAsync(db =>
        {
            db.Guilds.Add(new Guild { Id = guildId, Name = "Open Guild", IsRegistered = true, RosterMode = RosterMode.Open });
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId));
            db.GuildMemberships.Add(new GuildMembership { CharacterId = activeChar, GuildId = guildId, CharacterRank = CharacterRank.Main, JoinedAt = DateTime.UtcNow });
            db.GuildMemberships.Add(new GuildMembership { CharacterId = inactiveChar, GuildId = guildId, CharacterRank = CharacterRank.Main, JoinedAt = DateTime.UtcNow });
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/roster");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<GuildRosterMemberResponse>>(ApiJsonOptions);
        body.Should().ContainSingle(m => m.CharacterName == "Active");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds a user (if not already present) with an active character on an isolated realm slug.
    /// </summary>
    private async Task<int> SeedActiveCharacter(string discordId, long bnetCharacterId, string name, bool isActive = true)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaidOpsDbContext>();

        if (await db.Users.FindAsync(discordId) == null)
        {
            db.Users.Add(TestDataBuilder.CreateUser(discordId));
            await db.SaveChangesAsync();
        }

        var realm = TestDataBuilder.CreateRealm(slug: $"realm-gr-{bnetCharacterId}");
        db.Realms.Add(realm);
        await db.SaveChangesAsync();

        var character = TestDataBuilder.CreateCharacter(discordId, realm.Id, isActive: isActive, bnetCharacterId: bnetCharacterId, name: name);
        db.Characters.Add(character);
        await db.SaveChangesAsync();

        return character.Id;
    }
}
