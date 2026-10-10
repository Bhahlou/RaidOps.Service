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
/// All Discord IDs are in the 970... range and guild IDs in the 940... range to avoid primary-key
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
        var response = await Client.GetAsync("/api/v1/guilds/940000000000000001/roster?guildBranchId=1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Business logic ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetRoster_WhenGuildNotRegistered_Returns400()
    {
        const string id      = "970000000000000001";
        const string guildId = "940000000000000001";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: false));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/roster?guildBranchId=1");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetRoster_WhenBranchNotActive_Returns400()
    {
        const string id      = "970000000000000009";
        const string guildId = "940000000000000009";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/roster?guildBranchId=999999");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetRoster_WhenRequesterNotDiscordMember_Returns400()
    {
        const string id      = "970000000000000002";
        const string guildId = "940000000000000002";
        var branchId = await SeedGuildWithBranch(guildId, "Open Guild");
        await SeedAsync(db => { db.Users.Add(TestDataBuilder.CreateUser(id)); return Task.CompletedTask; });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/roster?guildBranchId={branchId}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetRoster_WhenRosterModeClosedWithoutRequiredRole_Returns400()
    {
        const string id      = "970000000000000003";
        const string guildId = "940000000000000003";
        var branchId = await SeedGuildWithBranch(guildId, "Closed Guild", rosterMode: RosterMode.DiscordRoleOnly, rosterRoleIds: ["role-1"]);
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/roster?guildBranchId={branchId}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetRoster_WhenRosterAccessGranted_Returns200WithMembersOrderedByRankThenName()
    {
        const string id      = "970000000000000004";
        const string guildId = "940000000000000004";
        var branchId = await SeedGuildWithBranch(guildId, "Open Guild");
        var mainZed = await SeedActiveCharacter(id, bnetCharacterId: 90004001, name: "Zed");
        var mainBob = await SeedActiveCharacter(id, bnetCharacterId: 90004002, name: "Bob");
        var altAaron = await SeedActiveCharacter(id, bnetCharacterId: 90004003, name: "Aaron");
        await SeedAsync(db =>
        {
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId));
            db.GuildMemberships.Add(new GuildMembership { CharacterId = mainZed, GuildId = guildId, GuildBranchId = branchId, CharacterRank = CharacterRank.Main, JoinedAt = DateTime.UtcNow });
            db.GuildMemberships.Add(new GuildMembership { CharacterId = mainBob, GuildId = guildId, GuildBranchId = branchId, CharacterRank = CharacterRank.Main, JoinedAt = DateTime.UtcNow });
            db.GuildMemberships.Add(new GuildMembership { CharacterId = altAaron, GuildId = guildId, GuildBranchId = branchId, CharacterRank = CharacterRank.Alt, JoinedAt = DateTime.UtcNow });
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/roster?guildBranchId={branchId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = (await response.Content.ReadFromJsonAsync<List<GuildRosterMemberResponse>>(ApiJsonOptions))!;
        body.Select(m => m.CharacterName).Should().Equal("Bob", "Zed", "Aaron");
        body[0].ClassId.Should().Be(8);
        body[0].RealmSlug.Should().NotBeNullOrEmpty();
        body[0].BranchName.Should().NotBeNullOrEmpty();
        body[0].PlayerDiscordId.Should().Be(id);
    }

    [Fact]
    public async Task GetRoster_ExcludesInactiveCharacters()
    {
        const string id      = "970000000000000005";
        const string guildId = "940000000000000005";
        var branchId = await SeedGuildWithBranch(guildId, "Open Guild");
        var activeChar = await SeedActiveCharacter(id, bnetCharacterId: 90005001, name: "Active");
        var inactiveChar = await SeedActiveCharacter(id, bnetCharacterId: 90005002, name: "Inactive", isActive: false);
        await SeedAsync(db =>
        {
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId));
            db.GuildMemberships.Add(new GuildMembership { CharacterId = activeChar, GuildId = guildId, GuildBranchId = branchId, CharacterRank = CharacterRank.Main, JoinedAt = DateTime.UtcNow });
            db.GuildMemberships.Add(new GuildMembership { CharacterId = inactiveChar, GuildId = guildId, GuildBranchId = branchId, CharacterRank = CharacterRank.Main, JoinedAt = DateTime.UtcNow });
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/roster?guildBranchId={branchId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<GuildRosterMemberResponse>>(ApiJsonOptions);
        body.Should().ContainSingle(m => m.CharacterName == "Active");
    }

    [Fact]
    public async Task GetRoster_ResolvesOwningPlayerDiscordDisplayInfo()
    {
        const string viewerId = "970000000000000006";
        const string ownerId  = "970000000000000106";
        const string guildId  = "940000000000000006";
        var branchId = await SeedGuildWithBranch(guildId, "Open Guild");
        var charId = await SeedActiveCharacter(ownerId, bnetCharacterId: 90006001, name: "Jaina");
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(viewerId));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(viewerId, guildId));
            db.GuildMemberships.Add(new GuildMembership { CharacterId = charId, GuildId = guildId, GuildBranchId = branchId, CharacterRank = CharacterRank.Main, JoinedAt = DateTime.UtcNow });
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: viewerId);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/roster?guildBranchId={branchId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<GuildRosterMemberResponse>>(ApiJsonOptions);
        var member = body.Should().ContainSingle(m => m.CharacterName == "Jaina").Which;
        member.PlayerDiscordId.Should().Be(ownerId);
        member.PlayerName.Should().Be("TestUser");
    }

    // ── CanExclude ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRoster_AdminRequester_MarksOthersRowAndOwnRowCanExcludeTrue()
    {
        const string adminId  = "970000000000000007";
        const string ownerId  = "970000000000000107";
        const string guildId  = "940000000000000007";
        var branchId = await SeedGuildWithBranch(guildId, "Test Guild");
        var adminChar = await SeedActiveCharacter(adminId, bnetCharacterId: 90007001, name: "AdminChar");
        var otherChar = await SeedActiveCharacter(ownerId, bnetCharacterId: 90007002, name: "OtherChar");
        await SeedAsync(db =>
        {
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(adminId, guildId, isAdmin: true));
            db.GuildMemberships.Add(new GuildMembership { CharacterId = adminChar, GuildId = guildId, GuildBranchId = branchId, CharacterRank = CharacterRank.Main, JoinedAt = DateTime.UtcNow });
            db.GuildMemberships.Add(new GuildMembership { CharacterId = otherChar, GuildId = guildId, GuildBranchId = branchId, CharacterRank = CharacterRank.Main, JoinedAt = DateTime.UtcNow });
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: adminId);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/roster?guildBranchId={branchId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = (await response.Content.ReadFromJsonAsync<List<GuildRosterMemberResponse>>(ApiJsonOptions))!;
        body.Should().OnlyContain(m => m.CanExclude);
    }

    [Fact]
    public async Task GetRoster_RosterOnlyRequester_MarksAllRowsCanExcludeFalse()
    {
        const string id      = "970000000000000008";
        const string guildId = "940000000000000008";
        var branchId = await SeedGuildWithBranch(guildId, "Test Guild");
        var ownChar = await SeedActiveCharacter(id, bnetCharacterId: 90008001, name: "OwnChar");
        await SeedAsync(db =>
        {
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId));
            db.GuildMemberships.Add(new GuildMembership { CharacterId = ownChar, GuildId = guildId, GuildBranchId = branchId, CharacterRank = CharacterRank.Main, JoinedAt = DateTime.UtcNow });
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/roster?guildBranchId={branchId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = (await response.Content.ReadFromJsonAsync<List<GuildRosterMemberResponse>>(ApiJsonOptions))!;
        body.Should().OnlyContain(m => !m.CanExclude);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds a registered guild with one active <see cref="GuildBranch"/> and returns its surrogate ID.
    /// </summary>
    private async Task<int> SeedGuildWithBranch(
        string guildId, string name, RosterMode? rosterMode = RosterMode.Open, List<string>? rosterRoleIds = null)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaidOpsDbContext>();

        db.Guilds.Add(new Guild { Id = guildId, Name = name, IsRegistered = true });
        var branch = TestDataBuilder.CreateGuildBranch(guildId, rosterMode: rosterMode, rosterRoleIds: rosterRoleIds);
        db.GuildBranches.Add(branch);
        await db.SaveChangesAsync();

        return branch.Id;
    }

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
