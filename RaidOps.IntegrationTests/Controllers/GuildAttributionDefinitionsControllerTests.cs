using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RaidOps.Application.Contracts.Raids.Attributions.Responses;
using RaidOps.Application.Contracts.Raids.Bosses.Responses;
using RaidOps.Application.Contracts.Raids.Spells.Responses;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids.Attributions;
using RaidOps.Domain.Models.Reference;
using RaidOps.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;

namespace RaidOps.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for <see cref="RaidOps.API.Controllers.v1.GuildAttributionDefinitionsController"/>.
/// All Discord IDs and guild IDs are in the 982… range to avoid primary-key conflicts with other
/// test classes (980… already belongs to <see cref="AvailabilityControllerTests"/>).
/// </summary>
[Collection("Integration")]
public class GuildAttributionDefinitionsControllerTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private static readonly object[] NameSlotCells =
    [
        new { kind = "NameSlot", iconSource = "None", slotLabel = "De", requiredClassIds = Array.Empty<int>(), requiredRoles = Array.Empty<string>(), requiredSpecIds = Array.Empty<int>() },
    ];

    private static readonly int[] SingleOrderedId = [1];

    // Branch 4 = "BC Classic (Anniv.)", CurrentExpansionId 2 — the expansion the seeded TBC spells/raid zones belong to.
    private const int BcClassicBranchId = 4;

    /// <summary>Seeds an active guild branch for an already-seeded guild and returns its surrogate ID (the route's <c>guildBranchId</c>).</summary>
    private async Task<int> SeedBranchAsync(string guildId)
    {
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var guildBranch = TestDataBuilder.CreateGuildBranch(guildId, branchId: BcClassicBranchId);
            db.GuildBranches.Add(guildBranch);
            await db.SaveChangesAsync();
            return guildBranch.Id;
        }
    }

    // ── Auth enforcement ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetDefinitions_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/guilds/982000000000000001/branches/1/attribution-definitions");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateDefinition_WithoutToken_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/guilds/982000000000000001/branches/1/attribution-definitions", new { label = "x", cells = NameSlotCells });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateDefinition_WithoutToken_Returns401()
    {
        var response = await Client.PatchAsync("/api/v1/guilds/982000000000000001/branches/1/attribution-definitions/1", JsonContent.Create(new { label = "x", cells = NameSlotCells }));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteDefinition_WithoutToken_Returns401()
    {
        var response = await Client.DeleteAsync("/api/v1/guilds/982000000000000001/branches/1/attribution-definitions/1");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ReorderDefinitions_WithoutToken_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/guilds/982000000000000001/branches/1/attribution-definitions/reorder", new { orderedIds = SingleOrderedId });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SearchSpells_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/guilds/982000000000000001/branches/1/spells/search?searchTerm=inn&locale=en");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetBossesForZone_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/guilds/982000000000000001/raid-zones/4/bosses");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SetSectionIcon_WithoutToken_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/guilds/982000000000000001/branches/1/attribution-definitions/sections/icon", new { section = "Interrupts", iconSource = "RaidMarker", raidMarker = "Skull" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetDefinitions_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.GetAsync("/api/v1/guilds/982000000000000001/branches/1/attribution-definitions");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GetDefinitions ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetDefinitions_WhenNotOfficer_Returns400()
    {
        const string id = "982000000000000002";
        const string guildId = "982000000000000002";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: false));
            return Task.CompletedTask;
        });
        var branchId = await SeedBranchAsync(guildId);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/attribution-definitions");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetDefinitions_WhenOfficer_ReturnsSeededDefinitionWithItsCells()
    {
        const string id = "982000000000000003";
        const string guildId = "982000000000000003";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            return Task.CompletedTask;
        });
        var branchId = await SeedBranchAsync(guildId);
        await SeedAsync(db =>
        {
            db.GuildAttributionDefinitions.Add(new GuildAttributionDefinition
            {
                GuildId = guildId,
                GuildBranchId = branchId,
                Label = "Innervate",
                Section = "Personals",
                IsRepeatable = true,
                SortOrder = 0,
                CreatedAt = DateTime.UtcNow,
                CreatedByDiscordId = id,
                Cells =
                [
                    new AttributionDefinitionCell { CellIndex = 0, Kind = AttributionCellKind.NameSlot, SlotLabel = "De" },
                ],
            });
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/attribution-definitions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var definitions = await response.Content.ReadFromJsonAsync<List<GuildAttributionDefinitionResponse>>(ApiJsonOptions);
        definitions.Should().ContainSingle();
        var definition = definitions!.Single();
        definition.Label.Should().Be("Innervate");
        definition.Section.Should().Be("Personals");
        definition.IsRepeatable.Should().BeTrue();
        definition.Cells.Should().ContainSingle(c => c.Kind == AttributionCellKind.NameSlot && c.SlotLabel == "De");
    }

    // ── GetBossesForZone ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetBossesForZone_WhenOfficer_ReturnsThatZonesBosses()
    {
        const string id = "982000000000000014";
        const string guildId = "982000000000000014";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/raid-zones/4/bosses"); // SSC

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var bosses = await response.Content.ReadFromJsonAsync<List<RaidBossResponse>>(ApiJsonOptions);
        bosses.Should().Contain(b => b.Id == 14 && b.Name == "Hydross the Unstable");
    }

    [Fact]
    public async Task GetBossesForZone_WhenNotOfficer_Returns400()
    {
        const string id = "982000000000000015";
        const string guildId = "982000000000000015";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: false));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/raid-zones/4/bosses");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── CreateDefinition ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreateDefinition_WhenOfficer_Returns200AndPersists()
    {
        const string id = "982000000000000004";
        const string guildId = "982000000000000004";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            return Task.CompletedTask;
        });
        var branchId = await SeedBranchAsync(guildId);
        var client = CreateAuthenticatedClient(discordId: id);
        var body = new { label = "Innervate", section = "Personals", isRepeatable = true, cells = NameSlotCells };

        var response = await client.PostAsJsonAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/attribution-definitions", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var definition = await db.GuildAttributionDefinitions.Include(d => d.Cells).FirstOrDefaultAsync(d => d.GuildId == guildId);
            definition.Should().NotBeNull();
            definition!.Label.Should().Be("Innervate");
            definition.Cells.Should().ContainSingle(c => c.SlotLabel == "De");
        }
    }

    [Fact]
    public async Task CreateDefinition_NoCells_Returns400WithNoCellsInDefinitionError()
    {
        const string id = "982000000000000005";
        const string guildId = "982000000000000005";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            return Task.CompletedTask;
        });
        var branchId = await SeedBranchAsync(guildId);
        var client = CreateAuthenticatedClient(discordId: id);
        var body = new { label = "Empty", cells = Array.Empty<object>() };

        var response = await client.PostAsJsonAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/attribution-definitions", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        json.GetProperty("error").GetString().Should().Be("NoCellsInDefinition");
    }

    // ── UpdateDefinition ─────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateDefinition_WhenOfficer_Returns200AndPersists()
    {
        const string id = "982000000000000006";
        const string guildId = "982000000000000006";
        int definitionId;
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            return Task.CompletedTask;
        });
        var branchId = await SeedBranchAsync(guildId);
        var (seedScope, seedDb) = CreateDbScope();
        using (seedScope)
        {
            var definition = new GuildAttributionDefinition
            {
                GuildId = guildId, GuildBranchId = branchId, Label = "Old label", SortOrder = 0, CreatedAt = DateTime.UtcNow, CreatedByDiscordId = id,
                Cells = [new AttributionDefinitionCell { CellIndex = 0, Kind = AttributionCellKind.NameSlot }],
            };
            seedDb.GuildAttributionDefinitions.Add(definition);
            await seedDb.SaveChangesAsync();
            definitionId = definition.Id;
        }
        var client = CreateAuthenticatedClient(discordId: id);
        var body = new { label = "New label", isRepeatable = true, cells = NameSlotCells };

        var response = await client.PatchAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/attribution-definitions/{definitionId}", JsonContent.Create(body));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var updated = await db.GuildAttributionDefinitions.FindAsync(definitionId);
            updated!.Label.Should().Be("New label");
            updated.IsRepeatable.Should().BeTrue();
        }
    }

    [Fact]
    public async Task UpdateDefinition_NotFound_Returns400()
    {
        const string id = "982000000000000007";
        const string guildId = "982000000000000007";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            return Task.CompletedTask;
        });
        var branchId = await SeedBranchAsync(guildId);
        var client = CreateAuthenticatedClient(discordId: id);
        var body = new { label = "x", cells = NameSlotCells };

        var response = await client.PatchAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/attribution-definitions/999999", JsonContent.Create(body));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── DeleteDefinition ─────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteDefinition_WhenOfficer_Returns200AndDeletes()
    {
        const string id = "982000000000000008";
        const string guildId = "982000000000000008";
        int definitionId;
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            return Task.CompletedTask;
        });
        var branchId = await SeedBranchAsync(guildId);
        var (seedScope, seedDb) = CreateDbScope();
        using (seedScope)
        {
            var definition = new GuildAttributionDefinition
            {
                GuildId = guildId, GuildBranchId = branchId, Label = "To delete", SortOrder = 0, CreatedAt = DateTime.UtcNow, CreatedByDiscordId = id,
                Cells = [new AttributionDefinitionCell { CellIndex = 0, Kind = AttributionCellKind.NameSlot }],
            };
            seedDb.GuildAttributionDefinitions.Add(definition);
            await seedDb.SaveChangesAsync();
            definitionId = definition.Id;
        }
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.DeleteAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/attribution-definitions/{definitionId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            (await db.GuildAttributionDefinitions.FindAsync(definitionId)).Should().BeNull();
        }
    }

    [Fact]
    public async Task DeleteDefinition_NotFound_Returns400()
    {
        const string id = "982000000000000009";
        const string guildId = "982000000000000009";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            return Task.CompletedTask;
        });
        var branchId = await SeedBranchAsync(guildId);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.DeleteAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/attribution-definitions/999999");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── ReorderDefinitions ───────────────────────────────────────────────────

    [Fact]
    public async Task ReorderDefinitions_WhenOfficer_Returns200AndPersistsOrder()
    {
        const string id = "982000000000000010";
        const string guildId = "982000000000000010";
        int firstId, secondId;
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            return Task.CompletedTask;
        });
        var branchId = await SeedBranchAsync(guildId);
        var (seedScope, seedDb) = CreateDbScope();
        using (seedScope)
        {
            var first = new GuildAttributionDefinition { GuildId = guildId, GuildBranchId = branchId, Label = "First", SortOrder = 0, CreatedAt = DateTime.UtcNow, CreatedByDiscordId = id, Cells = [new AttributionDefinitionCell { CellIndex = 0, Kind = AttributionCellKind.NameSlot }] };
            var second = new GuildAttributionDefinition { GuildId = guildId, GuildBranchId = branchId, Label = "Second", SortOrder = 1, CreatedAt = DateTime.UtcNow, CreatedByDiscordId = id, Cells = [new AttributionDefinitionCell { CellIndex = 0, Kind = AttributionCellKind.NameSlot }] };
            seedDb.GuildAttributionDefinitions.AddRange(first, second);
            await seedDb.SaveChangesAsync();
            firstId = first.Id;
            secondId = second.Id;
        }
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PostAsJsonAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/attribution-definitions/reorder", new { orderedIds = new[] { secondId, firstId } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            (await db.GuildAttributionDefinitions.FindAsync(secondId))!.SortOrder.Should().Be(0);
            (await db.GuildAttributionDefinitions.FindAsync(firstId))!.SortOrder.Should().Be(1);
        }
    }

    // ── SetSectionIcon ───────────────────────────────────────────────────────

    [Fact]
    public async Task SetSectionIcon_WhenOfficer_Returns200AndUpdatesEveryRowInThatSection()
    {
        const string id = "982000000000000016";
        const string guildId = "982000000000000016";
        int firstId, secondId;
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            return Task.CompletedTask;
        });
        var branchId = await SeedBranchAsync(guildId);
        var (seedScope, seedDb) = CreateDbScope();
        using (seedScope)
        {
            var first = new GuildAttributionDefinition { GuildId = guildId, GuildBranchId = branchId, Label = "Innervate", Section = "Personals", SortOrder = 0, CreatedAt = DateTime.UtcNow, CreatedByDiscordId = id, Cells = [new AttributionDefinitionCell { CellIndex = 0, Kind = AttributionCellKind.NameSlot }] };
            var second = new GuildAttributionDefinition { GuildId = guildId, GuildBranchId = branchId, Label = "PW: Shield", Section = "Personals", SortOrder = 1, CreatedAt = DateTime.UtcNow, CreatedByDiscordId = id, Cells = [new AttributionDefinitionCell { CellIndex = 0, Kind = AttributionCellKind.NameSlot }] };
            seedDb.GuildAttributionDefinitions.AddRange(first, second);
            await seedDb.SaveChangesAsync();
            firstId = first.Id;
            secondId = second.Id;
        }
        var client = CreateAuthenticatedClient(discordId: id);
        var body = new { section = "Personals", iconSource = "RaidMarker", raidMarker = "Skull" };

        var response = await client.PostAsJsonAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/attribution-definitions/sections/icon", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            (await db.GuildAttributionDefinitions.FindAsync(firstId))!.SectionIconSource.Should().Be(AttributionIconSource.RaidMarker);
            (await db.GuildAttributionDefinitions.FindAsync(secondId))!.SectionRaidMarker.Should().Be(RaidMarkerIcon.Skull);
        }
    }

    [Fact]
    public async Task SetSectionIcon_NoRowUsesThatSection_Returns400WithAttributionDefinitionNotFoundError()
    {
        const string id = "982000000000000017";
        const string guildId = "982000000000000017";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            return Task.CompletedTask;
        });
        var branchId = await SeedBranchAsync(guildId);
        var client = CreateAuthenticatedClient(discordId: id);
        var body = new { section = "No such section", iconSource = "RaidMarker", raidMarker = "Skull" };

        var response = await client.PostAsJsonAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/attribution-definitions/sections/icon", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        json.GetProperty("error").GetString().Should().Be("AttributionDefinitionNotFound");
    }

    // ── SearchSpells ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SearchSpells_WhenOfficer_ReturnsMatchingSpellLocalizedToRequesterLocale()
    {
        const string id = "982000000000000011";
        const string guildId = "982000000000000011";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            db.Spells.Add(new Spell { Id = 9980001 });
            db.SpellAvailabilities.Add(new SpellAvailability { SpellId = 9980001, ExpansionId = 2, NameEn = "Innervate", NameFr = "Vigueur naturelle", NameDe = "Winterschlaf", IconUrl = "https://cdn/innervate.jpg" });
            return Task.CompletedTask;
        });
        var branchId = await SeedBranchAsync(guildId);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/spells/search?searchTerm=vigueur&locale=fr");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var spells = await response.Content.ReadFromJsonAsync<List<SpellResponse>>();
        spells.Should().ContainSingle(s => s.Id == 9980001 && s.Name == "Vigueur naturelle");
    }

    // ── Token without a sub claim (every remaining action) ───────────────────

    [Fact]
    public async Task CreateDefinition_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.PostAsJsonAsync("/api/v1/guilds/982000000000000001/branches/1/attribution-definitions", new { label = "x", cells = NameSlotCells });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateDefinition_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.PatchAsync("/api/v1/guilds/982000000000000001/branches/1/attribution-definitions/1", JsonContent.Create(new { label = "x", cells = NameSlotCells }));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteDefinition_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.DeleteAsync("/api/v1/guilds/982000000000000001/branches/1/attribution-definitions/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ReorderDefinitions_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.PostAsJsonAsync("/api/v1/guilds/982000000000000001/branches/1/attribution-definitions/reorder", new { orderedIds = SingleOrderedId });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SetSectionIcon_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.PostAsJsonAsync("/api/v1/guilds/982000000000000001/branches/1/attribution-definitions/sections/icon", new { section = "Interrupts", iconSource = "RaidMarker", raidMarker = "Skull" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SearchSpells_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.GetAsync("/api/v1/guilds/982000000000000001/branches/1/spells/search?searchTerm=inn&locale=en");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetBossesForZone_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.GetAsync("/api/v1/guilds/982000000000000001/raid-zones/4/bosses");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Removed / unknown branch ─────────────────────────────────────────────

    [Fact]
    public async Task GetRaidZonesForGuild_RouteWasRemoved_Returns404()
    {
        const string id = "982000000000000030";
        const string guildId = "982000000000000030";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/raid-zones");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetDefinitions_GuildBranchThatDoesNotExist_Returns400()
    {
        const string id = "982000000000000031";
        const string guildId = "982000000000000031";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/branches/999999999/attribution-definitions");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
