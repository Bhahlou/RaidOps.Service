using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaidOps.Application.Contracts.Characters.Responses;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Discord;
using RaidOps.Infrastructure.Persistence.Implementations;
using RaidOps.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;

namespace RaidOps.IntegrationTests.Controllers;

[Collection("Integration")]
public class CharactersControllerTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private static readonly int[] SingleId = [1];
    private static readonly int[] NonExistentId = [99999];
    private static readonly int[] SingleViableSpec = [62];
    private static readonly int[] WrongClassViableSpec = [71];
    private static readonly int[] ViableSpecsExcludingMain = [62, 64];
    private static readonly int[] ThreeViableSpecs = [62, 63, 64];
    private static readonly int[] ReplacementViableSpec = [64];

    // ── Auth enforcement ────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/characters");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetSynced_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/characters/synced");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Sync_WithoutToken_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/characters/sync", new { BranchId = 1 });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Activate_WithoutToken_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/characters/activate", new { CharacterIds = SingleId });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Resync_WithoutToken_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/characters/1/resync", new { });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Deactivate_WithoutToken_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/characters/1/deactivate", new { });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SetRaidSpecs_WithoutToken_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/characters/1/raid-specs",
            new { mainSpecId = 62, viableSpecIds = SingleViableSpec });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCharacter_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/characters/retail/kazzak/arthas");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Business logic — GetAll / GetSynced ─────────────────────────────────

    [Fact]
    public async Task GetAll_WhenNoCharacters_ReturnsEmptyList()
    {
        const string id = "300000000000000001";
        await SeedAsync(db => { db.Users.Add(TestDataBuilder.CreateUser(id)); return Task.CompletedTask; });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync("/api/v1/characters");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetCharactersResponse>();
        body!.BnetAccount.Should().BeNull();
        body.Characters.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAll_WhenCharactersExist_ReturnsActiveCharacters()
    {
        const string id = "300000000000000011";
        var charId = await SeedUserWithActiveCharacter(id, bnetCharacterId: 90011);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync("/api/v1/characters");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetCharactersResponse>();
        body!.Characters.Should().ContainSingle(c => c.Id == charId && c.Name == "TestMage" && c.ClassName == "Mage");
    }

    [Fact]
    public async Task GetAll_WhenBnetLinkedAndCharacterInGuild_ReturnsBothEmbedded()
    {
        const string id = "300000000000000012";
        var charId = await SeedUserWithActiveCharacter(id, bnetCharacterId: 90012);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            db.BattleNetAccounts.Add(TestDataBuilder.CreateBnetAccount(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(id: "400000000000000001", name: "Dah Boo", isRegistered: true));
            db.GuildMemberships.Add(new GuildMembership
            {
                CharacterId = charId,
                GuildId = "400000000000000001",
                CharacterRank = CharacterRank.Main,
                JoinedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync("/api/v1/characters");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetCharactersResponse>(ApiJsonOptions);
        body!.BnetAccount.Should().NotBeNull();
        body.BnetAccount!.BattleTag.Should().Be("TestUser#1234");

        var character = body.Characters.Single(c => c.Id == charId);
        character.GuildMemberships.Should().ContainSingle(m =>
            m.GuildId == "400000000000000001" && m.GuildName == "Dah Boo" && m.CharacterRank == CharacterRank.Main);
    }

    [Fact]
    public async Task GetSynced_WhenNoCharacters_ReturnsEmptyList()
    {
        const string id = "300000000000000002";
        await SeedAsync(db => { db.Users.Add(TestDataBuilder.CreateUser(id)); return Task.CompletedTask; });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync("/api/v1/characters/synced");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<SyncedCharacterDto>>();
        body.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSynced_WhenCharactersExist_ReturnsMappedDtos()
    {
        const string id = "300000000000000013";
        await SeedUserWithActiveCharacter(id, bnetCharacterId: 90013);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync("/api/v1/characters/synced");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<SyncedCharacterDto>>();
        body.Should().ContainSingle(c => c.Name == "TestMage" && c.ClassName == "Mage" && c.IsActive);
    }

    // ── Business logic — Sync ───────────────────────────────────────────────

    [Fact]
    public async Task Sync_WithNoBnetAccount_Returns400()
    {
        const string id = "300000000000000020";
        await SeedAsync(db => { db.Users.Add(TestDataBuilder.CreateUser(id)); return Task.CompletedTask; });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PostAsJsonAsync("/api/v1/characters/sync", new { branchId = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Sync_WithInvalidBranchId_Returns400()
    {
        const string id = "300000000000000021";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.BattleNetAccounts.Add(TestDataBuilder.CreateBnetAccount(id));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PostAsJsonAsync("/api/v1/characters/sync", new { branchId = 9999 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Sync_WithValidBnetAccount_Returns200WithSyncCount()
    {
        const string id = "300000000000000022";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.BattleNetAccounts.Add(TestDataBuilder.CreateBnetAccount(id));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PostAsJsonAsync("/api/v1/characters/sync", new { branchId = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("synced successfully");
    }

    // ── Business logic — Activate ───────────────────────────────────────────

    [Fact]
    public async Task Activate_WithNoMatchingCharacters_Returns200()
    {
        const string id = "300000000000000030";
        await SeedAsync(db => { db.Users.Add(TestDataBuilder.CreateUser(id)); return Task.CompletedTask; });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PostAsJsonAsync("/api/v1/characters/activate",
            new { characterIds = NonExistentId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Activate_WithCharacters_Returns200AndSetsActive()
    {
        const string id = "300000000000000031";
        var charId = await SeedUserWithSyncedCharacter(id, bnetCharacterId: 90031);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PostAsJsonAsync("/api/v1/characters/activate",
            new { characterIds = new[] { charId } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var character = await db.Characters.FindAsync(charId);
            character!.IsActiveInRaidOps.Should().BeTrue();
        }
    }

    // ── Business logic — Deactivate ─────────────────────────────────────────

    [Fact]
    public async Task Deactivate_WhenCharacterExists_Returns200()
    {
        const string id = "300000000000000040";
        var charId = await SeedUserWithActiveCharacter(id, bnetCharacterId: 90040);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PostAsJsonAsync($"/api/v1/characters/{charId}/deactivate", new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Deactivate_WhenCharacterNotFound_Returns400()
    {
        const string id = "300000000000000041";
        await SeedAsync(db => { db.Users.Add(TestDataBuilder.CreateUser(id)); return Task.CompletedTask; });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PostAsJsonAsync("/api/v1/characters/99999/deactivate", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Business logic — Resync ─────────────────────────────────────────────

    [Fact]
    public async Task Resync_WhenCharacterNotFound_Returns400()
    {
        const string id = "300000000000000050";
        await SeedAsync(db => { db.Users.Add(TestDataBuilder.CreateUser(id)); return Task.CompletedTask; });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PostAsJsonAsync("/api/v1/characters/99999/resync", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Resync_WhenCharacterExists_Returns200WithDto()
    {
        const string id = "300000000000000051";
        var charId = await SeedUserWithActiveCharacter(id, bnetCharacterId: 90051);
        await SeedAsync(db =>
        {
            db.BattleNetAccounts.Add(TestDataBuilder.CreateBnetAccount(id));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PostAsJsonAsync($"/api/v1/characters/{charId}/resync", new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Business logic — SetRaidSpecs ───────────────────────────────────────

    [Fact]
    public async Task SetRaidSpecs_WhenCharacterNotFound_Returns400()
    {
        const string id = "300000000000000060";
        await SeedAsync(db => { db.Users.Add(TestDataBuilder.CreateUser(id)); return Task.CompletedTask; });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PostAsJsonAsync("/api/v1/characters/99999/raid-specs",
            new { mainSpecId = 62, viableSpecIds = SingleViableSpec });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SetRaidSpecs_WithSpecFromWrongClass_Returns400()
    {
        const string id = "300000000000000061";
        var charId = await SeedUserWithActiveCharacter(id, bnetCharacterId: 90061); // Mage (ClassId=8)
        var client = CreateAuthenticatedClient(discordId: id);

        // 71 = Arms (Warrior, ClassId=1) — invalid for a Mage character.
        var response = await client.PostAsJsonAsync($"/api/v1/characters/{charId}/raid-specs",
            new { mainSpecId = 71, viableSpecIds = WrongClassViableSpec });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SetRaidSpecs_WhenMainNotInViable_Returns400()
    {
        const string id = "300000000000000062";
        var charId = await SeedUserWithActiveCharacter(id, bnetCharacterId: 90062);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PostAsJsonAsync($"/api/v1/characters/{charId}/raid-specs",
            new { mainSpecId = 63, viableSpecIds = ViableSpecsExcludingMain });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SetRaidSpecs_WhenValid_Returns200AndPersistsExactlyOneMain()
    {
        const string id = "300000000000000063";
        var charId = await SeedUserWithActiveCharacter(id, bnetCharacterId: 90063);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PostAsJsonAsync($"/api/v1/characters/{charId}/raid-specs",
            new { mainSpecId = 63, viableSpecIds = ThreeViableSpecs });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var raidSpecs = db.CharacterRaidSpecs.Where(rs => rs.CharacterId == charId).ToList();
            raidSpecs.Should().HaveCount(3);
            raidSpecs.Should().ContainSingle(rs => rs.IsMain && rs.SpecId == 63);
        }
    }

    [Fact]
    public async Task SetRaidSpecs_CalledTwice_ReplacesExistingSpecs()
    {
        const string id = "300000000000000064";
        var charId = await SeedUserWithActiveCharacter(id, bnetCharacterId: 90064);
        var client = CreateAuthenticatedClient(discordId: id);

        await client.PostAsJsonAsync($"/api/v1/characters/{charId}/raid-specs",
            new { mainSpecId = 62, viableSpecIds = ThreeViableSpecs });

        var response = await client.PostAsJsonAsync($"/api/v1/characters/{charId}/raid-specs",
            new { mainSpecId = 64, viableSpecIds = ReplacementViableSpec });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var raidSpecs = db.CharacterRaidSpecs.Where(rs => rs.CharacterId == charId).ToList();
            raidSpecs.Should().ContainSingle();
            raidSpecs[0].SpecId.Should().Be(64);
            raidSpecs[0].IsMain.Should().BeTrue();
        }
    }

    [Fact]
    public async Task SetRaidSpecs_WhenValid_PersistsSpecNavigableBackToCharacter()
    {
        const string id = "300000000000000065";
        var charId = await SeedUserWithActiveCharacter(id, bnetCharacterId: 90065);
        var client = CreateAuthenticatedClient(discordId: id);

        await client.PostAsJsonAsync($"/api/v1/characters/{charId}/raid-specs",
            new { mainSpecId = 62, viableSpecIds = SingleViableSpec });

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var raidSpec = await db.CharacterRaidSpecs
                .Include(rs => rs.Character)
                .SingleAsync(rs => rs.CharacterId == charId);
            raidSpec.Character.Id.Should().Be(charId);
        }
    }

    // ── Business logic — GetCharacter ───────────────────────────────────────

    [Fact]
    public async Task GetCharacter_Owner_Returns200WithIsOwnerTrue()
    {
        const string id = "300000000000000070";
        var charId = await SeedUserWithActiveCharacter(id, bnetCharacterId: 90070);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync("/api/v1/characters/retail/realm-90070/testmage");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CharacterDetailResponse>(ApiJsonOptions);
        body!.Character.Id.Should().Be(charId);
        body.IsOwner.Should().BeTrue();
        body.CanEditRaidSpecs.Should().BeTrue();
    }

    [Fact]
    public async Task GetCharacter_UnknownCharacter_Returns400()
    {
        const string id = "300000000000000071";
        await SeedAsync(db => { db.Users.Add(TestDataBuilder.CreateUser(id)); return Task.CompletedTask; });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync("/api/v1/characters/retail/nowhere/nobody");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetCharacter_NotOwnerAndNoSharedGuild_Returns400()
    {
        const string ownerId  = "300000000000000072";
        const string strangerId = "300000000000000172";
        await SeedUserWithActiveCharacter(ownerId, bnetCharacterId: 90072);
        await SeedAsync(db => { db.Users.Add(TestDataBuilder.CreateUser(strangerId)); return Task.CompletedTask; });
        var client = CreateAuthenticatedClient(discordId: strangerId);

        var response = await client.GetAsync("/api/v1/characters/retail/realm-90072/testmage");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetCharacter_TeammateWithRosterAccess_Returns200ReadOnly()
    {
        const string ownerId    = "300000000000000073";
        const string teammateId = "300000000000000173";
        const string guildId    = "400000000000000073";
        var charId = await SeedUserWithActiveCharacter(ownerId, bnetCharacterId: 90073);
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(teammateId));
            db.Guilds.Add(new Guild { Id = guildId, Name = "Open Guild", IsRegistered = true, RosterMode = RosterMode.Open });
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(teammateId, guildId, isAdmin: false));
            db.GuildMemberships.Add(new GuildMembership { CharacterId = charId, GuildId = guildId, CharacterRank = CharacterRank.Main, JoinedAt = DateTime.UtcNow });
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: teammateId);

        var response = await client.GetAsync("/api/v1/characters/retail/realm-90073/testmage");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CharacterDetailResponse>(ApiJsonOptions);
        body!.IsOwner.Should().BeFalse();
        body.CanEditRaidSpecs.Should().BeFalse();
    }

    [Fact]
    public async Task GetCharacter_OfficerNotOwner_Returns200CanEditRaidSpecsTrue()
    {
        const string ownerId   = "300000000000000074";
        const string officerId = "300000000000000174";
        const string guildId   = "400000000000000074";
        var charId = await SeedUserWithActiveCharacter(ownerId, bnetCharacterId: 90074);
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(officerId));
            db.Guilds.Add(new Guild { Id = guildId, Name = "Open Guild", IsRegistered = true, RosterMode = RosterMode.Open });
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(officerId, guildId, isAdmin: true));
            db.GuildMemberships.Add(new GuildMembership { CharacterId = charId, GuildId = guildId, CharacterRank = CharacterRank.Main, JoinedAt = DateTime.UtcNow });
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: officerId);

        var response = await client.GetAsync("/api/v1/characters/retail/realm-90074/testmage");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CharacterDetailResponse>(ApiJsonOptions);
        body!.IsOwner.Should().BeFalse();
        body.CanEditRaidSpecs.Should().BeTrue();
    }

    // ── Business logic — SetRaidSpecs as officer ────────────────────────────

    [Fact]
    public async Task SetRaidSpecs_OfficerNotOwner_Returns200AndPersists()
    {
        const string ownerId   = "300000000000000075";
        const string officerId = "300000000000000175";
        const string guildId   = "400000000000000075";
        var charId = await SeedUserWithActiveCharacter(ownerId, bnetCharacterId: 90075);
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(officerId));
            db.Guilds.Add(new Guild { Id = guildId, Name = "Open Guild", IsRegistered = true, RosterMode = RosterMode.Open });
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(officerId, guildId, isAdmin: true));
            db.GuildMemberships.Add(new GuildMembership { CharacterId = charId, GuildId = guildId, CharacterRank = CharacterRank.Main, JoinedAt = DateTime.UtcNow });
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: officerId);

        var response = await client.PostAsJsonAsync($"/api/v1/characters/{charId}/raid-specs",
            new { mainSpecId = 62, viableSpecIds = SingleViableSpec });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var raidSpecs = db.CharacterRaidSpecs.Where(rs => rs.CharacterId == charId).ToList();
            raidSpecs.Should().ContainSingle(rs => rs.IsMain && rs.SpecId == 62);
        }
    }

    [Fact]
    public async Task SetRaidSpecs_StrangerNotOwnerNotOfficer_Returns400()
    {
        const string ownerId    = "300000000000000076";
        const string strangerId = "300000000000000176";
        var charId = await SeedUserWithActiveCharacter(ownerId, bnetCharacterId: 90076);
        await SeedAsync(db => { db.Users.Add(TestDataBuilder.CreateUser(strangerId)); return Task.CompletedTask; });
        var client = CreateAuthenticatedClient(discordId: strangerId);

        var response = await client.PostAsJsonAsync($"/api/v1/characters/{charId}/raid-specs",
            new { mainSpecId = 62, viableSpecIds = SingleViableSpec });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Domain — BnetCharacterSpec relationship ─────────────────────────────

    [Fact]
    public async Task BnetCharacterSpec_NavigatesBackToItsExpansionState()
    {
        const string id = "300000000000000066";
        var charId = await SeedUserWithActiveCharacter(id, bnetCharacterId: 90066);

        var (seedScope, seedDb) = CreateDbScope();
        using (seedScope)
        {
            var state = await seedDb.CharacterExpansionStates.SingleAsync(s => s.CharacterId == charId);
            seedDb.BnetCharacterSpecs.Add(new BnetCharacterSpec
            {
                CharacterExpansionStateId = state.Id,
                SpecId = 62,
                IsMain = true,
            });
            await seedDb.SaveChangesAsync();
        }

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var bnetSpec = await db.BnetCharacterSpecs
                .Include(s => s.CharacterExpansionState)
                .SingleAsync(s => s.SpecId == 62 && s.CharacterExpansionState.CharacterId == charId);
            bnetSpec.CharacterExpansionState.CharacterId.Should().Be(charId);
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<int> SeedUserWithActiveCharacter(string discordId, long bnetCharacterId = 90001)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaidOpsDbContext>();

        db.Users.Add(TestDataBuilder.CreateUser(discordId));
        await db.SaveChangesAsync();

        var realm = TestDataBuilder.CreateRealm(slug: $"realm-{bnetCharacterId}");
        db.Realms.Add(realm);
        await db.SaveChangesAsync();

        var character = TestDataBuilder.CreateCharacter(discordId, realm.Id, isActive: true, bnetCharacterId: bnetCharacterId);
        db.Characters.Add(character);
        await db.SaveChangesAsync();

        db.CharacterExpansionStates.Add(new CharacterExpansionState
        {
            CharacterId = character.Id,
            ExpansionId = 11,
            Level = 80,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        return character.Id;
    }

    private async Task<int> SeedUserWithSyncedCharacter(string discordId, long bnetCharacterId = 90001)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaidOpsDbContext>();

        db.Users.Add(TestDataBuilder.CreateUser(discordId));
        await db.SaveChangesAsync();

        var realm = TestDataBuilder.CreateRealm(slug: $"realm-sync-{bnetCharacterId}");
        db.Realms.Add(realm);
        await db.SaveChangesAsync();

        var character = TestDataBuilder.CreateCharacter(discordId, realm.Id, isActive: false, bnetCharacterId: bnetCharacterId);
        db.Characters.Add(character);
        await db.SaveChangesAsync();

        db.BattleNetAccounts.Add(TestDataBuilder.CreateBnetAccount(discordId));
        await db.SaveChangesAsync();

        return character.Id;
    }
}
