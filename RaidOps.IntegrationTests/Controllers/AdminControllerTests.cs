using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RaidOps.Application.Contracts.Raids.Buffs.Responses;
using RaidOps.Domain.Models.Reference;
using RaidOps.ExternalApplication.Contracts.Services.WagoTools.Responses;
using RaidOps.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RaidOps.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for <see cref="RaidOps.API.Controllers.v1.AdminController"/>, through the real HTTP
/// pipeline. The test host lists <see cref="RaidOpsWebApplicationFactory.OwnerDiscordId"/> in
/// <c>Admin:OwnerDiscordIds</c> and swaps wago.tools for a stub, so no real network is hit and — since
/// <c>Discord:SpellSyncChannelId</c> is unset — no Discord message is attempted. Every ID is in the 987…
/// range (spell IDs 9870001+) to avoid collisions with other test classes. The raid-buffs admin tests use
/// expansion 5 exclusively (spell IDs 9871001+) — its own, so a pruning import never touches another
/// class's rows (raid buff definitions are shared reference data scoped only by expansion, not by guild).
/// </summary>
[Collection("Integration")]
public class AdminControllerTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private const string SyncSpellsUrl = "/api/v1/admin/sync-spells";

    // Branch 4 = "Classic Anniversary": wago product "wow_anniversary", CurrentExpansionId 2.
    private const int AnniversaryBranchId = 4;
    private const int AnniversaryExpansionId = 2;
    private const string AnniversaryProduct = "wow_anniversary";
    private const int SyncedSpellId = 9870001;
    private const int ListfileSpellId = 9870002;
    private const string Build = "9.9.9.98765";

    private static readonly DateTime BuildDate = new(2026, 9, 24, 8, 30, 0, DateTimeKind.Utc);

    // ── Auth enforcement ─────────────────────────────────────────────────────

    [Fact]
    public async Task SyncSpells_WithoutToken_Returns401()
    {
        var response = await Client.PostAsync(SyncSpellsUrl, null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SyncSpells_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.PostAsync(SyncSpellsUrl, null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SyncSpells_AuthenticatedButNotAnOwner_Returns403AndSyncsNothing()
    {
        Factory.WagoStub.LatestBuilds = new() { [AnniversaryProduct] = new WagoBuildInfo { Product = AnniversaryProduct, Version = Build, CreatedAt = BuildDate } };
        try
        {
            var client = CreateAuthenticatedClient(discordId: "987000000000000002");

            var response = await client.PostAsync(SyncSpellsUrl, null);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            var (scope, db) = CreateDbScope();
            using (scope)
            {
                var branch = await db.Branches.AsNoTracking().SingleAsync(b => b.Id == AnniversaryBranchId);
                branch.LastSyncedBuildVersion.Should().BeNull();
            }
        }
        finally
        {
            Factory.WagoStub.Reset();
        }
    }

    // ── Owner ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SyncSpells_Owner_Returns200SyncsTheBranchAndForcesAResyncOfTheSameBuild()
    {
        var stub = Factory.WagoStub;
        stub.LatestBuilds = new() { [AnniversaryProduct] = new WagoBuildInfo { Product = AnniversaryProduct, Version = Build, CreatedAt = BuildDate } };
        stub.SpellNamesByLocale = new()
        {
            ["enUS"] = [new WagoSpellName { Id = SyncedSpellId, Name = "Admin Probe" }],
            ["frFR"] = [new WagoSpellName { Id = SyncedSpellId, Name = "Sonde admin" }],
            ["deDE"] = [new WagoSpellName { Id = SyncedSpellId, Name = "Admin-Sonde" }],
        };
        stub.SpellNamesByLocale["enUS"].Add(new WagoSpellName { Id = ListfileSpellId, Name = "Listfile Probe" });
        // One icon is too new for the community listfile (resolved by an individual lookup), the other is in it.
        stub.IconFileDataIds = new() { [SyncedSpellId] = 9870100, [ListfileSpellId] = 9870101 };
        stub.FileNames = new() { [9870100] = "interface/icons/inv_admin_probe.blp" };
        stub.IconFileNames = new() { [9870101] = "inv_listfile_probe" };
        var client = CreateAuthenticatedClient(discordId: RaidOpsWebApplicationFactory.OwnerDiscordId);

        try
        {
            // First run: the spell is new and the branch has never been synced.
            var first = await client.PostAsync(SyncSpellsUrl, null);

            first.StatusCode.Should().Be(HttpStatusCode.OK);
            var firstResult = await ReadSingleBranchResultAsync(first);
            firstResult.GetProperty("branchId").GetInt32().Should().Be(AnniversaryBranchId);
            firstResult.GetProperty("skipped").GetBoolean().Should().BeFalse();
            firstResult.GetProperty("latestBuild").GetString().Should().Be(Build);
            firstResult.GetProperty("addedCount").GetInt32().Should().Be(2);
            firstResult.GetProperty("renamedCount").GetInt32().Should().Be(0);

            var (scope, db) = CreateDbScope();
            using (scope)
            {
                var availability = await db.SpellAvailabilities.AsNoTracking().SingleAsync(a => a.SpellId == SyncedSpellId);
                availability.ExpansionId.Should().Be(AnniversaryExpansionId);
                availability.NameEn.Should().Be("Admin Probe");
                availability.NameFr.Should().Be("Sonde admin");
                availability.NameDe.Should().Be("Admin-Sonde");
                availability.IconUrl.Should().Be("https://render.worldofwarcraft.com/us/icons/56/inv_admin_probe.jpg");

                var fromListfile = await db.SpellAvailabilities.AsNoTracking().SingleAsync(a => a.SpellId == ListfileSpellId);
                fromListfile.IconUrl.Should().Be("https://render.worldofwarcraft.com/us/icons/56/inv_listfile_probe.jpg");

                var branch = await db.Branches.AsNoTracking().SingleAsync(b => b.Id == AnniversaryBranchId);
                branch.LastSyncedBuildVersion.Should().Be(Build);
                branch.LastSyncedBuildDate.Should().Be(BuildDate);
            }

            // Second run, same build, English name changed upstream: the admin trigger is Force = true, so the
            // branch is re-synced anyway (a non-forced run would report it as skipped) and reports the rename.
            stub.SpellNamesByLocale["enUS"] = [new WagoSpellName { Id = SyncedSpellId, Name = "Admin Probe Renamed" }];

            var second = await client.PostAsync(SyncSpellsUrl, null);

            second.StatusCode.Should().Be(HttpStatusCode.OK);
            var secondResult = await ReadSingleBranchResultAsync(second);
            secondResult.GetProperty("skipped").GetBoolean().Should().BeFalse();
            secondResult.GetProperty("previousBuild").GetString().Should().Be(Build);
            secondResult.GetProperty("addedCount").GetInt32().Should().Be(0);
            secondResult.GetProperty("renamedCount").GetInt32().Should().Be(1);

            var (scope2, db2) = CreateDbScope();
            using (scope2)
            {
                var renamed = await db2.SpellAvailabilities.AsNoTracking().SingleAsync(a => a.SpellId == SyncedSpellId);
                renamed.NameEn.Should().Be("Admin Probe Renamed");
            }
        }
        finally
        {
            stub.Reset();
            await SeedAsync(async db =>
            {
                await db.Spells.Where(s => s.Id == SyncedSpellId || s.Id == ListfileSpellId).ExecuteDeleteAsync();
                await db.Branches.Where(b => b.Id == AnniversaryBranchId)
                    .ExecuteUpdateAsync(s => s.SetProperty(b => b.LastSyncedBuildVersion, (string?)null).SetProperty(b => b.LastSyncedBuildDate, (DateTime?)null));
            });
        }
    }

    [Fact]
    public async Task SyncSpells_OwnerWhenWagoTracksNoneOfOurProducts_Returns200WithNoBranchResults()
    {
        var client = CreateAuthenticatedClient(discordId: RaidOpsWebApplicationFactory.OwnerDiscordId);

        var response = await client.PostAsync(SyncSpellsUrl, null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("body").GetArrayLength().Should().Be(0);
    }

    private static async Task<JsonElement> ReadSingleBranchResultAsync(HttpResponseMessage response)
    {
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var results = json.RootElement.GetProperty("body");
        results.GetArrayLength().Should().Be(1);
        return results[0].Clone();
    }

    // ── Raid buffs ───────────────────────────────────────────────────────────

    private const int RaidBuffExpansionId = 5;

    private static object MakeSource(int classId = 7, int? specId = 264) => new { classId, specId };

    private static object MakeRaidBuffBody(int spellId, string label = "+1 zqx", string? exclusiveGroupKey = null, string? capacityPoolKey = null, params object[] sources) => new
    {
        spellId,
        scope = "Raid",
        kind = "Buff",
        labelEn = label,
        labelFr = label,
        labelDe = label,
        exclusiveGroupKey,
        capacityPoolKey,
        sortOrder = 0,
        sources = sources.Length > 0 ? sources : new object[] { MakeSource() },
    };

    private async Task EnsureRaidBuffSpellAsync(int spellId)
    {
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            if (await db.Spells.AnyAsync(s => s.Id == spellId))
                return;
        }

        await SeedAsync(db =>
        {
            db.Spells.Add(new Spell { Id = spellId });
            db.SpellAvailabilities.Add(new SpellAvailability { SpellId = spellId, ExpansionId = RaidBuffExpansionId, NameEn = $"Zqx {spellId}", NameFr = $"Zqx {spellId}", NameDe = $"Zqx {spellId}", IconUrl = "https://cdn/zqx.jpg" });
            return Task.CompletedTask;
        });
    }

    private static async Task<List<RaidBuffDefinitionResponse>> GetRaidBuffsAsync(HttpClient client, int expansionId = RaidBuffExpansionId)
        => (await client.GetFromJsonAsync<List<RaidBuffDefinitionResponse>>($"/api/v1/raidbuffs?expansionId={expansionId}", ApiJsonOptions))!;

    // -- SaveRaidBuff --

    [Fact]
    public async Task SaveRaidBuff_WithoutToken_Returns401()
    {
        var response = await Client.PutAsJsonAsync($"/api/v1/admin/raid-buffs/{RaidBuffExpansionId}", MakeRaidBuffBody(9871001));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SaveRaidBuff_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.PutAsJsonAsync($"/api/v1/admin/raid-buffs/{RaidBuffExpansionId}", MakeRaidBuffBody(9871001));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SaveRaidBuff_AuthenticatedButNotAnOwner_Returns403()
    {
        var client = CreateAuthenticatedClient(discordId: "987000000000000003");

        var response = await client.PutAsJsonAsync($"/api/v1/admin/raid-buffs/{RaidBuffExpansionId}", MakeRaidBuffBody(9871001));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SaveRaidBuff_Owner_CreatesTheDefinitionVisibleOnThePublicReadEndpoint()
    {
        const int spellId = 9871001;
        await EnsureRaidBuffSpellAsync(spellId);
        var client = CreateAuthenticatedClient(discordId: RaidOpsWebApplicationFactory.OwnerDiscordId);

        var response = await client.PutAsJsonAsync($"/api/v1/admin/raid-buffs/{RaidBuffExpansionId}", MakeRaidBuffBody(spellId, label: "+1 zqx save"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("body").GetProperty("created").GetInt32().Should().Be(1);

        var definitions = await GetRaidBuffsAsync(client);
        definitions.Should().ContainSingle(d => d.SpellId == spellId && d.LabelEn == "+1 zqx save");
    }

    [Fact]
    public async Task SaveRaidBuff_SpellNotKnownOnTheExpansion_Returns400WithTheValidatorDetail()
    {
        var client = CreateAuthenticatedClient(discordId: RaidOpsWebApplicationFactory.OwnerDiscordId);

        var response = await client.PutAsJsonAsync($"/api/v1/admin/raid-buffs/{RaidBuffExpansionId}", MakeRaidBuffBody(999999999));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("detail").GetString().Should().Contain("is not known on this expansion");
    }

    // -- ImportRaidBuffs --

    [Fact]
    public async Task ImportRaidBuffs_WithoutToken_Returns401()
    {
        var response = await Client.PostAsJsonAsync($"/api/v1/admin/raid-buffs/{RaidBuffExpansionId}/import", new { definitions = new[] { MakeRaidBuffBody(9871010) }, pruneMissing = false });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ImportRaidBuffs_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.PostAsJsonAsync($"/api/v1/admin/raid-buffs/{RaidBuffExpansionId}/import", new { definitions = new[] { MakeRaidBuffBody(9871010) }, pruneMissing = false });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ImportRaidBuffs_AuthenticatedButNotAnOwner_Returns403()
    {
        var client = CreateAuthenticatedClient(discordId: "987000000000000003");

        var response = await client.PostAsJsonAsync($"/api/v1/admin/raid-buffs/{RaidBuffExpansionId}/import", new { definitions = new[] { MakeRaidBuffBody(9871010) }, pruneMissing = false });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ImportRaidBuffs_Owner_WithPruneMissing_DeletesWhateverTheFileDoesNotList()
    {
        const int keptSpellId = 9871010;
        const int prunedSpellId = 9871011;
        await EnsureRaidBuffSpellAsync(keptSpellId);
        await EnsureRaidBuffSpellAsync(prunedSpellId);
        var client = CreateAuthenticatedClient(discordId: RaidOpsWebApplicationFactory.OwnerDiscordId);
        await client.PutAsJsonAsync($"/api/v1/admin/raid-buffs/{RaidBuffExpansionId}", MakeRaidBuffBody(keptSpellId, label: "keep"));
        await client.PutAsJsonAsync($"/api/v1/admin/raid-buffs/{RaidBuffExpansionId}", MakeRaidBuffBody(prunedSpellId, label: "prune me"));

        var response = await client.PostAsJsonAsync(
            $"/api/v1/admin/raid-buffs/{RaidBuffExpansionId}/import",
            new { definitions = new[] { MakeRaidBuffBody(keptSpellId, label: "keep, updated") }, pruneMissing = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var summary = body.RootElement.GetProperty("body");
        summary.GetProperty("updated").GetInt32().Should().Be(1);
        summary.GetProperty("deleted").GetInt32().Should().Be(1);

        var definitions = await GetRaidBuffsAsync(client);
        definitions.Should().ContainSingle(d => d.SpellId == keptSpellId && d.LabelEn == "keep, updated");
        definitions.Should().NotContain(d => d.SpellId == prunedSpellId);
    }

    [Fact]
    public async Task ImportRaidBuffs_Owner_WithoutPruneMissing_LeavesOtherDefinitionsUntouched()
    {
        const int existingSpellId = 9871012;
        const int newSpellId = 9871013;
        await EnsureRaidBuffSpellAsync(existingSpellId);
        await EnsureRaidBuffSpellAsync(newSpellId);
        var client = CreateAuthenticatedClient(discordId: RaidOpsWebApplicationFactory.OwnerDiscordId);
        await client.PutAsJsonAsync($"/api/v1/admin/raid-buffs/{RaidBuffExpansionId}", MakeRaidBuffBody(existingSpellId));

        var response = await client.PostAsJsonAsync(
            $"/api/v1/admin/raid-buffs/{RaidBuffExpansionId}/import",
            new { definitions = new[] { MakeRaidBuffBody(newSpellId) }, pruneMissing = false });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var definitions = await GetRaidBuffsAsync(client);
        definitions.Should().Contain(d => d.SpellId == existingSpellId).And.Contain(d => d.SpellId == newSpellId);
    }

    // -- UpdateRaidBuff --

    [Fact]
    public async Task UpdateRaidBuff_WithoutToken_Returns401()
    {
        var response = await Client.PutAsJsonAsync("/api/v1/admin/raid-buffs/definitions/1", MakeRaidBuffBody(9871001));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateRaidBuff_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.PutAsJsonAsync("/api/v1/admin/raid-buffs/definitions/1", MakeRaidBuffBody(9871001));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateRaidBuff_AuthenticatedButNotAnOwner_Returns403()
    {
        var client = CreateAuthenticatedClient(discordId: "987000000000000003");

        var response = await client.PutAsJsonAsync("/api/v1/admin/raid-buffs/definitions/1", MakeRaidBuffBody(9871001));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateRaidBuff_UnknownId_Returns400()
    {
        var client = CreateAuthenticatedClient(discordId: RaidOpsWebApplicationFactory.OwnerDiscordId);

        var response = await client.PutAsJsonAsync("/api/v1/admin/raid-buffs/definitions/999999999", MakeRaidBuffBody(9871001));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be("NotFound");
    }

    [Fact]
    public async Task UpdateRaidBuff_Owner_ChangesTheLabelAndTheSpellInPlace()
    {
        const int originalSpellId = 9871020;
        const int newSpellId = 9871021;
        await EnsureRaidBuffSpellAsync(originalSpellId);
        await EnsureRaidBuffSpellAsync(newSpellId);
        var client = CreateAuthenticatedClient(discordId: RaidOpsWebApplicationFactory.OwnerDiscordId);
        await client.PutAsJsonAsync($"/api/v1/admin/raid-buffs/{RaidBuffExpansionId}", MakeRaidBuffBody(originalSpellId, label: "original"));
        var definitionId = (await GetRaidBuffsAsync(client)).Single(d => d.SpellId == originalSpellId).Id;

        var response = await client.PutAsJsonAsync($"/api/v1/admin/raid-buffs/definitions/{definitionId}", MakeRaidBuffBody(newSpellId, label: "renamed and re-spelled"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var definitions = await GetRaidBuffsAsync(client);
        definitions.Should().ContainSingle(d => d.Id == definitionId && d.SpellId == newSpellId && d.LabelEn == "renamed and re-spelled");
        definitions.Should().NotContain(d => d.SpellId == originalSpellId);
    }

    [Fact]
    public async Task UpdateRaidBuff_SwitchingToASpellAlreadyUsedByAnotherDefinition_Returns400()
    {
        const int firstSpellId = 9871030;
        const int secondSpellId = 9871031;
        await EnsureRaidBuffSpellAsync(firstSpellId);
        await EnsureRaidBuffSpellAsync(secondSpellId);
        var client = CreateAuthenticatedClient(discordId: RaidOpsWebApplicationFactory.OwnerDiscordId);
        await client.PutAsJsonAsync($"/api/v1/admin/raid-buffs/{RaidBuffExpansionId}", MakeRaidBuffBody(firstSpellId));
        await client.PutAsJsonAsync($"/api/v1/admin/raid-buffs/{RaidBuffExpansionId}", MakeRaidBuffBody(secondSpellId));
        var secondDefinitionId = (await GetRaidBuffsAsync(client)).Single(d => d.SpellId == secondSpellId).Id;

        var response = await client.PutAsJsonAsync($"/api/v1/admin/raid-buffs/definitions/{secondDefinitionId}", MakeRaidBuffBody(firstSpellId));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("detail").GetString().Should().Contain("already has a definition");
    }

    // -- DeleteRaidBuff --

    [Fact]
    public async Task DeleteRaidBuff_WithoutToken_Returns401()
    {
        var response = await Client.DeleteAsync("/api/v1/admin/raid-buffs/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteRaidBuff_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.DeleteAsync("/api/v1/admin/raid-buffs/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteRaidBuff_AuthenticatedButNotAnOwner_Returns403()
    {
        var client = CreateAuthenticatedClient(discordId: "987000000000000003");

        var response = await client.DeleteAsync("/api/v1/admin/raid-buffs/1");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteRaidBuff_UnknownId_Returns400()
    {
        var client = CreateAuthenticatedClient(discordId: RaidOpsWebApplicationFactory.OwnerDiscordId);

        var response = await client.DeleteAsync("/api/v1/admin/raid-buffs/999999999");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteRaidBuff_Owner_RemovesItFromThePublicReadEndpoint()
    {
        const int spellId = 9871040;
        await EnsureRaidBuffSpellAsync(spellId);
        var client = CreateAuthenticatedClient(discordId: RaidOpsWebApplicationFactory.OwnerDiscordId);
        await client.PutAsJsonAsync($"/api/v1/admin/raid-buffs/{RaidBuffExpansionId}", MakeRaidBuffBody(spellId));
        var definitionId = (await GetRaidBuffsAsync(client)).Single(d => d.SpellId == spellId).Id;

        var response = await client.DeleteAsync($"/api/v1/admin/raid-buffs/{definitionId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetRaidBuffsAsync(client)).Should().NotContain(d => d.SpellId == spellId);
    }
}
