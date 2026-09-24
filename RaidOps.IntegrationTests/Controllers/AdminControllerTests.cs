using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RaidOps.ExternalApplication.Contracts.Services.WagoTools.Responses;
using RaidOps.IntegrationTests.Infrastructure;
using System.Net;
using System.Text.Json;

namespace RaidOps.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for <see cref="RaidOps.API.Controllers.v1.AdminController"/>, through the real HTTP
/// pipeline. The test host lists <see cref="RaidOpsWebApplicationFactory.OwnerDiscordId"/> in
/// <c>Admin:OwnerDiscordIds</c> and swaps wago.tools for a stub, so no real network is hit and — since
/// <c>Discord:SpellSyncChannelId</c> is unset — no Discord message is attempted. Every ID is in the 987…
/// range (spell IDs 9870001+) to avoid collisions with other test classes.
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
        stub.IconFileDataIds = new() { [SyncedSpellId] = 9870100 };
        stub.FileNames = new() { [9870100] = "interface/icons/inv_admin_probe.blp" };
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
            firstResult.GetProperty("addedCount").GetInt32().Should().Be(1);
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
                await db.Spells.Where(s => s.Id == SyncedSpellId).ExecuteDeleteAsync();
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
}
