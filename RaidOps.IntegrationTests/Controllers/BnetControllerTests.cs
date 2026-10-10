using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RaidOps.Application.Contracts.Characters.Responses;
using RaidOps.Domain.Models.Discord;
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
    public async Task GetAccounts_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/bnet/accounts");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Unlink_WithoutToken_Returns401()
    {
        var response = await Client.DeleteAsync("/api/v1/bnet/accounts/987654321");
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

    // ── GetAccounts ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAccounts_WhenNoAccountLinked_ReturnsEmptyArray()
    {
        const string id = "400000000000000002";
        await SeedAsync(db => { db.Users.Add(TestDataBuilder.CreateUser(id)); return Task.CompletedTask; });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync("/api/v1/bnet/accounts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<BnetAccountResponse>>();
        body.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAccounts_WhenOneAccountLinked_ReturnsIt()
    {
        const string id = "400000000000000003";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.BattleNetAccounts.Add(TestDataBuilder.CreateBnetAccount(id, "Bhahlou#1234"));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync("/api/v1/bnet/accounts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<BnetAccountResponse>>();
        body.Should().ContainSingle();
        body![0].BattleTag.Should().Be("Bhahlou#1234");
        body[0].Region.Should().Be("eu");
    }

    [Fact]
    public async Task GetAccounts_WhenMultipleAccountsLinked_ReturnsAllOfThem()
    {
        const string id = "400000000000000009";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.BattleNetAccounts.Add(TestDataBuilder.CreateBnetAccount(id, "Bhahlou#1234", bnetId: "111"));
            db.BattleNetAccounts.Add(TestDataBuilder.CreateBnetAccount(id, "Bhahlou#5678", bnetId: "222"));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync("/api/v1/bnet/accounts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<BnetAccountResponse>>();
        body.Should().HaveCount(2);
        body!.Select(a => a.BattleTag).Should().BeEquivalentTo(["Bhahlou#1234", "Bhahlou#5678"]);
    }

    // ── Unlink ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Unlink_ExistingAccount_ReturnsOkAndRemovesIt()
    {
        const string id = "400000000000000010";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.BattleNetAccounts.Add(TestDataBuilder.CreateBnetAccount(id, bnetId: "111"));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.DeleteAsync("/api/v1/bnet/accounts/111");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var remaining = await db.BattleNetAccounts.Where(a => a.UserDiscordId == id).ToListAsync();
            remaining.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task Unlink_NonExistentAccount_StillReturnsOk()
    {
        const string id = "400000000000000011";
        await SeedAsync(db => { db.Users.Add(TestDataBuilder.CreateUser(id)); return Task.CompletedTask; });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.DeleteAsync("/api/v1/bnet/accounts/does-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Unlink_HardDeletesCharactersSourcedFromThatAccountOnly()
    {
        const string id = "400000000000000012";
        await SeedAsync(async db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.BattleNetAccounts.Add(TestDataBuilder.CreateBnetAccount(id, bnetId: "111"));
            db.BattleNetAccounts.Add(TestDataBuilder.CreateBnetAccount(id, "Kept#0001", bnetId: "222"));
            var realm = TestDataBuilder.CreateRealm(slug: "unlink-test-realm");
            db.Realms.Add(realm);
            await db.SaveChangesAsync();

            var toDelete = TestDataBuilder.CreateCharacter(
                id, realm.Id, isActive: true, name: "ToDelete", bnetCharacterId: 91001, sourceBnetId: "111");
            var toKeep = TestDataBuilder.CreateCharacter(
                id, realm.Id, isActive: true, name: "ToKeep", bnetCharacterId: 91002, sourceBnetId: "222");
            db.Characters.Add(toDelete);
            db.Characters.Add(toKeep);
            await db.SaveChangesAsync();
        });

        var client = CreateAuthenticatedClient(discordId: id);
        var response = await client.DeleteAsync("/api/v1/bnet/accounts/111");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var remaining = await db.Characters.Where(c => c.UserDiscordId == id).ToListAsync();
            remaining.Should().ContainSingle();
            remaining[0].Name.Should().Be("ToKeep");
        }
    }

    [Fact]
    public async Task Unlink_CharacterInAGuild_LogsMemberLeftAuditEntry()
    {
        const string id = "400000000000000013";
        const string guildId = "500000000000000001";
        await SeedAsync(async db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.BattleNetAccounts.Add(TestDataBuilder.CreateBnetAccount(id, bnetId: "111"));
            db.Guilds.Add(TestDataBuilder.CreateGuild(id: guildId, name: "Test Guild", isRegistered: true));
            var branch = TestDataBuilder.CreateGuildBranch(guildId);
            db.GuildBranches.Add(branch);
            var realm = TestDataBuilder.CreateRealm(slug: "unlink-audit-realm");
            db.Realms.Add(realm);
            await db.SaveChangesAsync();

            var character = TestDataBuilder.CreateCharacter(
                id, realm.Id, isActive: true, name: "Arthas", bnetCharacterId: 91003, sourceBnetId: "111");
            db.Characters.Add(character);
            await db.SaveChangesAsync();

            db.GuildMemberships.Add(new GuildMembership { CharacterId = character.Id, GuildId = guildId, GuildBranch = branch, JoinedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        });

        var client = CreateAuthenticatedClient(discordId: id);
        var response = await client.DeleteAsync("/api/v1/bnet/accounts/111");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var auditEntries = await db.GuildAuditLogs.Where(l => l.GuildId == guildId).ToListAsync();
            auditEntries.Should().ContainSingle();
            auditEntries[0].ActorDiscordId.Should().Be(id);
        }
    }

    // ── Initiate / Callback (unchanged behavior) ─────────────────────────────

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
