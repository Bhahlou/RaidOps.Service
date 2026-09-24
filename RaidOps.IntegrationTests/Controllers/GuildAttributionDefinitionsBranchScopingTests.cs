using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RaidOps.Application.Contracts.Raids.Attributions.Responses;
using RaidOps.Application.Contracts.Raids.Spells.Responses;
using RaidOps.Domain.Models.Reference;
using RaidOps.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RaidOps.IntegrationTests.Controllers;

/// <summary>
/// End-to-end tests (HTTP → handlers → real Postgres) proving that a guild running two branches gets two
/// independent attribution templates, and that the spell catalogue is resolved per branch expansion.
/// One guild runs Classic Anniversary (branch 4, expansion 2 = TBC) and Forever (branch 5, expansion 12).
/// All guild/user IDs are in the 988… range; spell IDs are 9880001+.
/// </summary>
[Collection("Integration")]
public class GuildAttributionDefinitionsBranchScopingTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private const int AnniversaryWowBranchId = 4;
    private const int TbcExpansionId = 2;
    private const int ForeverWowBranchId = 5;
    private const int ForeverExpansionId = 12;

    /// <summary>A spell that exists on both expansions under different names and icons.</summary>
    private const int SharedSpellId = 9880001;

    /// <summary>A spell that only exists on Forever's expansion.</summary>
    private const int ForeverOnlySpellId = 9880002;

    private sealed record Scenario(string GuildId, string OfficerId, int AnniversaryBranch, int ForeverBranch, HttpClient Officer);

    private static readonly object[] NameSlotCells =
    [
        new { kind = "NameSlot", iconSource = "None", slotLabel = "De", requiredClassIds = Array.Empty<int>(), requiredRoles = Array.Empty<string>(), requiredSpecIds = Array.Empty<int>() },
    ];

    private static object[] SpellIconCells(int spellId) =>
    [
        new { kind = "Icon", iconSource = "Spell", spellId, requiredClassIds = Array.Empty<int>(), requiredRoles = Array.Empty<string>(), requiredSpecIds = Array.Empty<int>() },
    ];

    private async Task<Scenario> SeedScenarioAsync(string id, bool withSpells = false)
    {
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(id, isRegistered: true));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, id, isAdmin: true));
            return Task.CompletedTask;
        });

        var anniversary = TestDataBuilder.CreateGuildBranch(id, branchId: AnniversaryWowBranchId);
        var forever = TestDataBuilder.CreateGuildBranch(id, branchId: ForeverWowBranchId);
        await SeedAsync(db =>
        {
            db.GuildBranches.AddRange(anniversary, forever);
            return Task.CompletedTask;
        });

        if (withSpells)
            await EnsureSpellsAsync();

        return new Scenario(id, id, anniversary.Id, forever.Id, CreateAuthenticatedClient(discordId: id));
    }

    /// <summary>Idempotently seeds the two shared test spells (several tests use them; the DB is shared).</summary>
    private async Task EnsureSpellsAsync()
    {
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            if (await db.Spells.AnyAsync(s => s.Id == SharedSpellId))
                return;

            db.Spells.AddRange(new Spell { Id = SharedSpellId }, new Spell { Id = ForeverOnlySpellId });
            db.SpellAvailabilities.AddRange(
                new SpellAvailability { SpellId = SharedSpellId, ExpansionId = TbcExpansionId, NameEn = "Zqxbranch Tbc Name", NameFr = "Zqxbranch Nom Tbc", NameDe = "Zqxbranch Tbc Name De", IconUrl = "https://cdn/shared-tbc.jpg" },
                new SpellAvailability { SpellId = SharedSpellId, ExpansionId = ForeverExpansionId, NameEn = "Zqxbranch Forever Name", NameFr = "Zqxbranch Nom Forever", NameDe = "Zqxbranch Forever Name De", IconUrl = "https://cdn/shared-forever.jpg" },
                new SpellAvailability { SpellId = ForeverOnlySpellId, ExpansionId = ForeverExpansionId, NameEn = "Zqxbranch Forever Only", NameFr = "Zqxbranch Forever Seul", NameDe = "Zqxbranch Nur Forever", IconUrl = "https://cdn/forever-only.jpg" });
            await db.SaveChangesAsync();
        }
    }

    private static string DefinitionsUrl(Scenario s, int branch) => $"/api/v1/guilds/{s.GuildId}/branches/{branch}/attribution-definitions";

    private static async Task<List<GuildAttributionDefinitionResponse>> GetDefinitionsAsync(Scenario s, int branch)
    {
        var response = await s.Officer.GetAsync(DefinitionsUrl(s, branch));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<List<GuildAttributionDefinitionResponse>>(ApiJsonOptions))!;
    }

    private static async Task<string?> ReadErrorAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("error").GetString();
    }

    // ── Template isolation ───────────────────────────────────────────────────

    [Fact]
    public async Task CreateDefinition_OnBranchA_IsVisibleOnBranchAAndInvisibleFromBranchB()
    {
        var s = await SeedScenarioAsync("988000000000000001");

        var create = await s.Officer.PostAsJsonAsync(DefinitionsUrl(s, s.AnniversaryBranch), new { label = "A only", section = "Personals", cells = NameSlotCells });

        create.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetDefinitionsAsync(s, s.AnniversaryBranch)).Should().ContainSingle(d => d.Label == "A only");
        (await GetDefinitionsAsync(s, s.ForeverBranch)).Should().BeEmpty();
        var (scope, db) = CreateDbScope();
        using (scope)
            (await db.GuildAttributionDefinitions.SingleAsync(d => d.GuildId == s.GuildId)).GuildBranchId.Should().Be(s.AnniversaryBranch);
    }

    [Fact]
    public async Task BothBranchesCanHoldTheirOwnRowsWithTheSameLabel()
    {
        var s = await SeedScenarioAsync("988000000000000002");

        await s.Officer.PostAsJsonAsync(DefinitionsUrl(s, s.AnniversaryBranch), new { label = "Interrupt", section = "Utility", cells = NameSlotCells });
        await s.Officer.PostAsJsonAsync(DefinitionsUrl(s, s.ForeverBranch), new { label = "Interrupt", section = "Utility", cells = NameSlotCells });

        var a = await GetDefinitionsAsync(s, s.AnniversaryBranch);
        var b = await GetDefinitionsAsync(s, s.ForeverBranch);
        a.Should().ContainSingle();
        b.Should().ContainSingle();
        a.Single().Id.Should().NotBe(b.Single().Id);
        a.Single().SortOrder.Should().Be(0);
        b.Single().SortOrder.Should().Be(0);
    }

    [Fact]
    public async Task UpdateAndDelete_ThroughTheOtherBranchsRoute_AreRejectedAndChangeNothing()
    {
        var s = await SeedScenarioAsync("988000000000000003");
        await s.Officer.PostAsJsonAsync(DefinitionsUrl(s, s.AnniversaryBranch), new { label = "Keep me", section = "Personals", cells = NameSlotCells });
        var definitionId = (await GetDefinitionsAsync(s, s.AnniversaryBranch)).Single().Id;

        var update = await s.Officer.PatchAsync($"{DefinitionsUrl(s, s.ForeverBranch)}/{definitionId}", JsonContent.Create(new { label = "Hijacked", cells = NameSlotCells }));
        var delete = await s.Officer.DeleteAsync($"{DefinitionsUrl(s, s.ForeverBranch)}/{definitionId}");

        update.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadErrorAsync(update)).Should().Be("AttributionDefinitionNotFound");
        delete.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadErrorAsync(delete)).Should().Be("AttributionDefinitionNotFound");
        (await GetDefinitionsAsync(s, s.AnniversaryBranch)).Should().ContainSingle(d => d.Id == definitionId && d.Label == "Keep me");
    }

    [Fact]
    public async Task ReorderThroughBranchB_DoesNotReorderBranchAsRows()
    {
        var s = await SeedScenarioAsync("988000000000000004");
        foreach (var label in new[] { "First", "Second" })
            await s.Officer.PostAsJsonAsync(DefinitionsUrl(s, s.AnniversaryBranch), new { label, cells = NameSlotCells });
        var ids = (await GetDefinitionsAsync(s, s.AnniversaryBranch)).Select(d => d.Id).ToList();

        var response = await s.Officer.PostAsJsonAsync($"{DefinitionsUrl(s, s.ForeverBranch)}/reorder", new { orderedIds = new[] { ids[1], ids[0] } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetDefinitionsAsync(s, s.AnniversaryBranch)).Select(d => d.Label).Should().Equal("First", "Second");
    }

    [Fact]
    public async Task ReorderThroughItsOwnBranch_ReordersTheRows()
    {
        var s = await SeedScenarioAsync("988000000000000005");
        foreach (var label in new[] { "First", "Second" })
            await s.Officer.PostAsJsonAsync(DefinitionsUrl(s, s.AnniversaryBranch), new { label, cells = NameSlotCells });
        var ids = (await GetDefinitionsAsync(s, s.AnniversaryBranch)).Select(d => d.Id).ToList();

        var response = await s.Officer.PostAsJsonAsync($"{DefinitionsUrl(s, s.AnniversaryBranch)}/reorder", new { orderedIds = new[] { ids[1], ids[0] } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetDefinitionsAsync(s, s.AnniversaryBranch)).Select(d => d.Label).Should().Equal("Second", "First");
    }

    [Fact]
    public async Task SetSectionIcon_OnlyAffectsTheBranchItIsCalledOn()
    {
        var s = await SeedScenarioAsync("988000000000000006");
        await s.Officer.PostAsJsonAsync(DefinitionsUrl(s, s.AnniversaryBranch), new { label = "A row", section = "Interrupts", cells = NameSlotCells });
        await s.Officer.PostAsJsonAsync(DefinitionsUrl(s, s.ForeverBranch), new { label = "B row", section = "Interrupts", cells = NameSlotCells });

        var response = await s.Officer.PostAsJsonAsync($"{DefinitionsUrl(s, s.AnniversaryBranch)}/sections/icon", new { section = "Interrupts", iconSource = "RaidMarker", raidMarker = "Skull" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetDefinitionsAsync(s, s.AnniversaryBranch)).Single().SectionRaidMarker.Should().NotBeNull();
        (await GetDefinitionsAsync(s, s.ForeverBranch)).Single().SectionRaidMarker.Should().BeNull();
    }

    [Fact]
    public async Task SetSectionIcon_ForASectionThatOnlyExistsOnTheOtherBranch_Returns400()
    {
        var s = await SeedScenarioAsync("988000000000000007");
        await s.Officer.PostAsJsonAsync(DefinitionsUrl(s, s.AnniversaryBranch), new { label = "A row", section = "Interrupts", cells = NameSlotCells });

        var response = await s.Officer.PostAsJsonAsync($"{DefinitionsUrl(s, s.ForeverBranch)}/sections/icon", new { section = "Interrupts", iconSource = "RaidMarker", raidMarker = "Skull" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadErrorAsync(response)).Should().Be("AttributionDefinitionNotFound");
    }

    // ── Spells per expansion ─────────────────────────────────────────────────

    [Fact]
    public async Task SearchSpells_ReturnsTheNameAndIconOfTheBranchsOwnExpansion()
    {
        var s = await SeedScenarioAsync("988000000000000008", withSpells: true);

        var onAnniversary = await s.Officer.GetFromJsonAsync<List<SpellResponse>>($"/api/v1/guilds/{s.GuildId}/branches/{s.AnniversaryBranch}/spells/search?searchTerm=zqxbranch&locale=en");
        var onForever = await s.Officer.GetFromJsonAsync<List<SpellResponse>>($"/api/v1/guilds/{s.GuildId}/branches/{s.ForeverBranch}/spells/search?searchTerm=zqxbranch&locale=en");
        var onForeverFr = await s.Officer.GetFromJsonAsync<List<SpellResponse>>($"/api/v1/guilds/{s.GuildId}/branches/{s.ForeverBranch}/spells/search?searchTerm=zqxbranch&locale=fr");

        onAnniversary.Should().ContainSingle().Which.Should().BeEquivalentTo(new SpellResponse { Id = SharedSpellId, Name = "Zqxbranch Tbc Name", IconUrl = "https://cdn/shared-tbc.jpg" });
        onForever!.Select(x => (x.Id, x.Name, x.IconUrl)).Should().Equal(
            (SharedSpellId, "Zqxbranch Forever Name", "https://cdn/shared-forever.jpg"),
            (ForeverOnlySpellId, "Zqxbranch Forever Only", "https://cdn/forever-only.jpg"));
        onForeverFr!.Select(x => x.Name).Should().Equal("Zqxbranch Forever Seul", "Zqxbranch Nom Forever");
    }

    [Fact]
    public async Task CreateDefinition_SpellThatOnlyExistsOnForever_IsRejectedOnAnniversaryAndAcceptedOnForever()
    {
        var s = await SeedScenarioAsync("988000000000000009", withSpells: true);

        var onAnniversary = await s.Officer.PostAsJsonAsync(DefinitionsUrl(s, s.AnniversaryBranch), new { label = "Forever spell", cells = SpellIconCells(ForeverOnlySpellId) });
        var onForever = await s.Officer.PostAsJsonAsync(DefinitionsUrl(s, s.ForeverBranch), new { label = "Forever spell", cells = SpellIconCells(ForeverOnlySpellId) });

        onAnniversary.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadErrorAsync(onAnniversary)).Should().Be("SpellNotFound");
        onForever.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetDefinitionsAsync(s, s.AnniversaryBranch)).Should().BeEmpty();
        (await GetDefinitionsAsync(s, s.ForeverBranch)).Should().ContainSingle();
    }

    [Fact]
    public async Task UpdateDefinition_SwitchingToASpellUnavailableOnTheBranch_IsRejected()
    {
        var s = await SeedScenarioAsync("988000000000000010", withSpells: true);
        await s.Officer.PostAsJsonAsync(DefinitionsUrl(s, s.AnniversaryBranch), new { label = "Row", cells = NameSlotCells });
        var definitionId = (await GetDefinitionsAsync(s, s.AnniversaryBranch)).Single().Id;

        var response = await s.Officer.PatchAsync($"{DefinitionsUrl(s, s.AnniversaryBranch)}/{definitionId}", JsonContent.Create(new { label = "Row", cells = SpellIconCells(ForeverOnlySpellId) }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadErrorAsync(response)).Should().Be("SpellNotFound");
    }

    [Fact]
    public async Task SetSectionIcon_WithASpellUnavailableOnTheBranch_IsRejected()
    {
        var s = await SeedScenarioAsync("988000000000000011", withSpells: true);
        await s.Officer.PostAsJsonAsync(DefinitionsUrl(s, s.AnniversaryBranch), new { label = "Row", section = "Cooldowns", cells = NameSlotCells });

        var response = await s.Officer.PostAsJsonAsync($"{DefinitionsUrl(s, s.AnniversaryBranch)}/sections/icon", new { section = "Cooldowns", iconSource = "Spell", spellId = ForeverOnlySpellId });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadErrorAsync(response)).Should().Be("SpellNotFound");
    }

    [Fact]
    public async Task GetDefinitions_ResolvesTheSameSpellsIconOnEachBranchsOwnExpansion()
    {
        var s = await SeedScenarioAsync("988000000000000012", withSpells: true);
        await s.Officer.PostAsJsonAsync(DefinitionsUrl(s, s.AnniversaryBranch), new { label = "Bloodlust", section = "Cooldowns", cells = SpellIconCells(SharedSpellId) });
        await s.Officer.PostAsJsonAsync(DefinitionsUrl(s, s.ForeverBranch), new { label = "Bloodlust", section = "Cooldowns", cells = SpellIconCells(SharedSpellId) });
        await s.Officer.PostAsJsonAsync($"{DefinitionsUrl(s, s.ForeverBranch)}/sections/icon", new { section = "Cooldowns", iconSource = "Spell", spellId = SharedSpellId });

        var onAnniversary = await GetDefinitionsAsync(s, s.AnniversaryBranch);
        var onForever = await GetDefinitionsAsync(s, s.ForeverBranch);

        onAnniversary.Single().Cells.Single().SpellIconUrl.Should().Be("https://cdn/shared-tbc.jpg");
        onForever.Single().Cells.Single().SpellIconUrl.Should().Be("https://cdn/shared-forever.jpg");
        onForever.Single().SectionSpellIconUrl.Should().Be("https://cdn/shared-forever.jpg");
    }

    // ── Access control per branch ────────────────────────────────────────────

    [Fact]
    public async Task GetDefinitions_GuildBranchBelongingToAnotherGuild_Returns400()
    {
        var s = await SeedScenarioAsync("988000000000000013");
        var other = await SeedScenarioAsync("988000000000000014");

        var response = await s.Officer.GetAsync($"/api/v1/guilds/{s.GuildId}/branches/{other.AnniversaryBranch}/attribution-definitions");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
