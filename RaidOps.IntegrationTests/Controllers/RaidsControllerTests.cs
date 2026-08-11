using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Implementations;
using RaidOps.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RaidOps.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for <see cref="RaidOps.API.Controllers.v1.RaidsController"/>.
/// Uses a shared Testcontainers PostgreSQL instance via <see cref="RaidOpsWebApplicationFactory"/>.
/// All Discord IDs are in the 610… range and guild IDs in the 630… range to avoid primary-key
/// conflicts with other test classes. Branch 4 ("Classic Anniversary") is used throughout since
/// it's the only WoW branch with raid zones seeded (ExpansionId 2 — see <c>SeedRaidZones</c>).
/// </summary>
[Collection("Integration")]
public class RaidsControllerTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private const int RaidBranchId = 4;
    private const int KarazhanZoneId = 1;
    private static readonly int[] DefaultZoneIds = [KarazhanZoneId];
    private static readonly int[] GruulsLairZoneIds = [2];

    // ── Auth enforcement ────────────────────────────────────────────────────

    [Fact]
    public async Task GetZonesForBranch_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/guilds/630000000000000001/branches/1/raids/zones");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetLockoutWeek_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/guilds/630000000000000001/branches/1/raids/lockout-week");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetSeriesList_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/guilds/630000000000000001/branches/1/raids/series");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateSeries_WithoutToken_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/guilds/630000000000000001/branches/1/raids/series", new { });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateSeries_WithoutToken_Returns401()
    {
        var response = await Client.PatchAsync("/api/v1/guilds/630000000000000001/branches/1/raids/series/1", JsonContent.Create(new { }));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeactivateSeries_WithoutToken_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/guilds/630000000000000001/branches/1/raids/series/1/deactivate", new { });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MaterializeOccurrences_WithoutToken_Returns401()
    {
        var response = await Client.PostAsync("/api/v1/guilds/630000000000000001/branches/1/raids/materialize?rangeStart=2026-01-01&rangeEnd=2026-01-07", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetBoard_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/guilds/630000000000000001/branches/1/raids/board?rangeStart=2026-01-01&rangeEnd=2026-01-07");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateEvent_WithoutToken_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/guilds/630000000000000001/branches/1/raids/events", new { });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateEvent_WithoutToken_Returns401()
    {
        var response = await Client.PatchAsync("/api/v1/guilds/630000000000000001/branches/1/raids/events/1", JsonContent.Create(new { }));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteEvent_WithoutToken_Returns401()
    {
        var response = await Client.DeleteAsync("/api/v1/guilds/630000000000000001/branches/1/raids/events/1");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PublishEvent_WithoutToken_Returns401()
    {
        var response = await Client.PostAsync("/api/v1/guilds/630000000000000001/branches/1/raids/events/1/publish", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AssignSlot_WithoutToken_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/guilds/630000000000000001/branches/1/raids/events/1/slots/assign", new { });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SwapSlotAssignments_WithoutToken_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/guilds/630000000000000001/branches/1/raids/events/1/slots/swap", new { });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UnassignSlot_WithoutToken_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/guilds/630000000000000001/branches/1/raids/events/1/slots/unassign", new { });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateSlotAssignmentSpec_WithoutToken_Returns401()
    {
        var response = await Client.PatchAsync("/api/v1/guilds/630000000000000001/branches/1/raids/events/1/slots/spec", JsonContent.Create(new { }));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUnassignedMembers_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/guilds/630000000000000001/branches/1/raids/unassigned-members?rangeStart=2026-01-01&rangeEnd=2026-01-07");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetEventSummary_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/guilds/630000000000000001/branches/1/raids/events/1");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAssignedCharacters_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/guilds/630000000000000001/branches/1/raids/events/1/assigned-characters");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AnnounceGrouping_WithoutToken_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/guilds/630000000000000001/branches/1/raids/events/1/announce-grouping", new { });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetZonesForBranch_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.GetAsync("/api/v1/guilds/630000000000000001/branches/1/raids/zones");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetLockoutWeek_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.GetAsync("/api/v1/guilds/630000000000000001/branches/1/raids/lockout-week");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetSeriesList_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.GetAsync("/api/v1/guilds/630000000000000001/branches/1/raids/series");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateSeries_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.PostAsJsonAsync("/api/v1/guilds/630000000000000001/branches/1/raids/series", new
        {
            name = "Split 1",
            recurrenceDayOfWeek = DayOfWeek.Wednesday,
            recurrenceStartTimeLocal = new TimeOnly(20, 0),
            groupCount = 2,
            slotsPerGroup = 5,
            raidZoneIds = DefaultZoneIds,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateSeries_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.PatchAsync("/api/v1/guilds/630000000000000001/branches/1/raids/series/1", JsonContent.Create(new
        {
            name = "Split 1",
            recurrenceDayOfWeek = DayOfWeek.Wednesday,
            recurrenceStartTimeLocal = new TimeOnly(20, 0),
            groupCount = 2,
            slotsPerGroup = 5,
            raidZoneIds = DefaultZoneIds,
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeactivateSeries_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.PostAsJsonAsync("/api/v1/guilds/630000000000000001/branches/1/raids/series/1/deactivate", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MaterializeOccurrences_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.PostAsync("/api/v1/guilds/630000000000000001/branches/1/raids/materialize?rangeStart=2026-01-01&rangeEnd=2026-01-07", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetBoard_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.GetAsync("/api/v1/guilds/630000000000000001/branches/1/raids/board?rangeStart=2026-01-01&rangeEnd=2026-01-07");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateEvent_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.PostAsJsonAsync("/api/v1/guilds/630000000000000001/branches/1/raids/events", new
        {
            name = "One-shot event",
            startsAtUtc = DateTime.UtcNow.AddDays(3),
            groupCount = 2,
            slotsPerGroup = 5,
            raidZoneIds = DefaultZoneIds,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateEvent_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.PatchAsync("/api/v1/guilds/630000000000000001/branches/1/raids/events/1", JsonContent.Create(new
        {
            name = "One-shot event",
            startsAtUtc = DateTime.UtcNow.AddDays(3),
            groupCount = 2,
            slotsPerGroup = 5,
            raidZoneIds = DefaultZoneIds,
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteEvent_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.DeleteAsync("/api/v1/guilds/630000000000000001/branches/1/raids/events/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PublishEvent_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.PostAsync("/api/v1/guilds/630000000000000001/branches/1/raids/events/1/publish", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AssignSlot_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.PostAsJsonAsync("/api/v1/guilds/630000000000000001/branches/1/raids/events/1/slots/assign", new
        {
            groupNumber = 1, slotNumber = 1, characterId = 1,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SwapSlotAssignments_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.PostAsJsonAsync("/api/v1/guilds/630000000000000001/branches/1/raids/events/1/slots/swap", new
        {
            groupNumberA = 1, slotNumberA = 1, groupNumberB = 2, slotNumberB = 1,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UnassignSlot_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.PostAsJsonAsync("/api/v1/guilds/630000000000000001/branches/1/raids/events/1/slots/unassign", new
        {
            groupNumber = 1, slotNumber = 1,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateSlotAssignmentSpec_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.PatchAsync("/api/v1/guilds/630000000000000001/branches/1/raids/events/1/slots/spec", JsonContent.Create(new
        {
            groupNumber = 1, slotNumber = 1, specId = 1,
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUnassignedMembers_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.GetAsync("/api/v1/guilds/630000000000000001/branches/1/raids/unassigned-members?rangeStart=2026-01-01&rangeEnd=2026-01-07");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetEventSummary_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.GetAsync("/api/v1/guilds/630000000000000001/branches/1/raids/events/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAssignedCharacters_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.GetAsync("/api/v1/guilds/630000000000000001/branches/1/raids/events/1/assigned-characters");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AnnounceGrouping_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.PostAsJsonAsync("/api/v1/guilds/630000000000000001/branches/1/raids/events/1/announce-grouping", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Zones ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetZonesForBranch_Returns200WithZonesSeededForBranchsExpansion()
    {
        const string id = "610000000000000001";
        const string guildId = "630000000000000001";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/raids/zones");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var zones = json.EnumerateArray().ToList();
        zones.Should().HaveCount(8);
        zones.Should().Contain(z => z.GetProperty("name").GetString() == "Karazhan");
    }

    // ── Lockout week ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetLockoutWeek_NoRegionConfigured_ReturnsNullWeek()
    {
        const string id = "610000000000000002";
        const string guildId = "630000000000000002";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/raids/lockout-week");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("weekStartLocal").ValueKind.Should().Be(JsonValueKind.Null);
        json.GetProperty("weekEndLocal").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetLockoutWeek_RegionConfigured_ReturnsResolvedWeek()
    {
        const string id = "610000000000000003";
        const string guildId = "630000000000000003";
        var branchId = await SeedGuildAndBranch(id, guildId, region: "eu");
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/raids/lockout-week");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("weekStartLocal").ValueKind.Should().Be(JsonValueKind.String);
        json.GetProperty("weekEndLocal").ValueKind.Should().Be(JsonValueKind.String);
    }

    // ── Series ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateSeries_Persists_ReturnsCreatedId()
    {
        const string id = "610000000000000010";
        const string guildId = "630000000000000010";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PostAsJsonAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/raids/series", new
        {
            name = "Split 1",
            recurrenceDayOfWeek = DayOfWeek.Wednesday,
            recurrenceStartTimeLocal = new TimeOnly(20, 0),
            recurrenceIntervalWeeks = 1,
            groupCount = 2,
            slotsPerGroup = 5,
            raidZoneIds = new[] { KarazhanZoneId },
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var seriesId = json.GetProperty("body").GetProperty("id").GetInt32();

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var series = await db.RaidSeries.Include(s => s.DefaultZones).FirstAsync(s => s.Id == seriesId);
            series.Name.Should().Be("Split 1");
            series.IsActive.Should().BeTrue();
            series.DefaultZones.Should().ContainSingle(z => z.RaidZoneId == KarazhanZoneId);
        }
    }

    [Fact]
    public async Task CreateSeries_InvalidGridShape_Returns400()
    {
        const string id = "610000000000000011";
        const string guildId = "630000000000000011";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PostAsJsonAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/raids/series", new
        {
            name = "Split 1",
            recurrenceDayOfWeek = DayOfWeek.Wednesday,
            recurrenceStartTimeLocal = new TimeOnly(20, 0),
            groupCount = 0,
            slotsPerGroup = 5,
            raidZoneIds = new[] { KarazhanZoneId },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be("InvalidRequest");
    }

    [Fact]
    public async Task GetSeriesList_ReturnsCreatedSeries()
    {
        const string id = "610000000000000012";
        const string guildId = "630000000000000012";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);
        var seriesId = await CreateSeriesAsync(client, guildId, branchId);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/raids/series");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.EnumerateArray().Should().ContainSingle(s => s.GetProperty("id").GetInt32() == seriesId);
    }

    [Fact]
    public async Task UpdateSeries_ReplacesFieldsAndZoneSet()
    {
        const string id = "610000000000000013";
        const string guildId = "630000000000000013";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);
        var seriesId = await CreateSeriesAsync(client, guildId, branchId, zoneIds: [KarazhanZoneId]);

        var response = await client.PatchAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/raids/series/{seriesId}", JsonContent.Create(new
        {
            name = "Split 1 (renamed)",
            recurrenceDayOfWeek = DayOfWeek.Friday,
            recurrenceStartTimeLocal = new TimeOnly(21, 0),
            recurrenceIntervalWeeks = 2,
            groupCount = 3,
            slotsPerGroup = 5,
            raidZoneIds = GruulsLairZoneIds,
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var series = await db.RaidSeries.Include(s => s.DefaultZones).FirstAsync(s => s.Id == seriesId);
            series.Name.Should().Be("Split 1 (renamed)");
            series.RecurrenceDayOfWeek.Should().Be(DayOfWeek.Friday);
            series.GroupCount.Should().Be(3);
            series.DefaultZones.Should().ContainSingle(z => z.RaidZoneId == 2);
        }
    }

    [Fact]
    public async Task UpdateSeries_SeriesNotFound_Returns400()
    {
        const string id = "610000000000000015";
        const string guildId = "630000000000000015";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PatchAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/raids/series/999999", JsonContent.Create(new
        {
            name = "Split 1",
            recurrenceDayOfWeek = DayOfWeek.Wednesday,
            recurrenceStartTimeLocal = new TimeOnly(20, 0),
            recurrenceIntervalWeeks = 1,
            groupCount = 2,
            slotsPerGroup = 5,
            raidZoneIds = DefaultZoneIds,
        }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be("RaidSeriesNotFound");
    }

    [Fact]
    public async Task DeactivateSeries_WithDeleteEmptyOccurrences_BulkDeletesEmptyDraftsButKeepsPublishedOrAssigned()
    {
        const string id = "610000000000000014";
        const string guildId = "630000000000000014";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);
        var seriesId = await CreateSeriesAsync(client, guildId, branchId);

        int emptyDraftEventId;
        int publishedEventId;
        var (seedScope, seedDb) = CreateDbScope();
        using (seedScope)
        {
            var emptyDraft = new RaidEvent
            {
                GuildId = guildId, GuildBranchId = branchId, RaidSeriesId = seriesId, Name = "Empty draft",
                StartsAtUtc = DateTime.UtcNow, GroupCount = 2, SlotsPerGroup = 5,
                CreatedByDiscordId = id, CreatedAt = DateTime.UtcNow,
                TargetZones = [new RaidEventZone { RaidZoneId = KarazhanZoneId }],
            };
            var published = new RaidEvent
            {
                GuildId = guildId, GuildBranchId = branchId, RaidSeriesId = seriesId, Name = "Published",
                StartsAtUtc = DateTime.UtcNow, GroupCount = 2, SlotsPerGroup = 5,
                CreatedByDiscordId = id, CreatedAt = DateTime.UtcNow,
                PublicationStatus = RaidPublicationStatus.Published,
                TargetZones = [new RaidEventZone { RaidZoneId = KarazhanZoneId }],
            };
            seedDb.RaidEvents.AddRange(emptyDraft, published);
            await seedDb.SaveChangesAsync();
            emptyDraftEventId = emptyDraft.Id;
            publishedEventId = published.Id;
        }

        var response = await client.PostAsJsonAsync(
            $"/api/v1/guilds/{guildId}/branches/{branchId}/raids/series/{seriesId}/deactivate",
            new { deleteEmptyOccurrences = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("body").GetProperty("deletedCount").GetInt32().Should().Be(1);

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var series = await db.RaidSeries.FirstAsync(s => s.Id == seriesId);
            series.IsActive.Should().BeFalse();

            (await db.RaidEvents.AnyAsync(e => e.Id == emptyDraftEventId)).Should().BeFalse();
            (await db.RaidEvents.AnyAsync(e => e.Id == publishedEventId)).Should().BeTrue();
        }
    }

    // ── Materialize + board ─────────────────────────────────────────────────

    [Fact]
    public async Task MaterializeOccurrences_CreatesEventFromActiveSeriesWithinRange()
    {
        const string id = "610000000000000020";
        const string guildId = "630000000000000020";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);
        // Any day of week qualifies with RecurrenceIntervalWeeks = 1 as long as it falls once in range.
        var seriesId = await CreateSeriesAsync(client, guildId, branchId, dayOfWeek: DayOfWeek.Wednesday);

        var rangeStart = NextDateOnOrAfter(DateOnly.FromDateTime(DateTime.UtcNow), DayOfWeek.Monday);
        var rangeEnd = rangeStart.AddDays(6);

        var response = await client.PostAsync(
            $"/api/v1/guilds/{guildId}/branches/{branchId}/raids/materialize?rangeStart={rangeStart:yyyy-MM-dd}&rangeEnd={rangeEnd:yyyy-MM-dd}",
            null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("body").GetProperty("materializedCount").GetInt32().Should().Be(1);

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var events = await db.RaidEvents.Where(e => e.RaidSeriesId == seriesId).ToListAsync();
            events.Should().ContainSingle();
        }
    }

    [Fact]
    public async Task MaterializeOccurrences_CalledTwiceForSameRange_IsIdempotent()
    {
        const string id = "610000000000000021";
        const string guildId = "630000000000000021";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);
        var seriesId = await CreateSeriesAsync(client, guildId, branchId, dayOfWeek: DayOfWeek.Wednesday);
        var rangeStart = NextDateOnOrAfter(DateOnly.FromDateTime(DateTime.UtcNow), DayOfWeek.Monday);
        var rangeEnd = rangeStart.AddDays(6);
        var url = $"/api/v1/guilds/{guildId}/branches/{branchId}/raids/materialize?rangeStart={rangeStart:yyyy-MM-dd}&rangeEnd={rangeEnd:yyyy-MM-dd}";

        await client.PostAsync(url, null);
        var second = await client.PostAsync(url, null);

        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await second.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("body").GetProperty("materializedCount").GetInt32().Should().Be(0);

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var events = await db.RaidEvents.Where(e => e.RaidSeriesId == seriesId).ToListAsync();
            events.Should().ContainSingle();
        }
    }

    [Fact]
    public async Task GetBoard_ReturnsCreatedEventWithZonesAndAssignments()
    {
        const string id = "610000000000000022";
        const string guildId = "630000000000000022";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);
        var characterId = await SeedCharacterOnRoster(id, guildId, branchId, bnetCharacterId: 610022001);
        var eventId = await CreateAdhocEventAsync(client, guildId, branchId, startsAtUtc: DateTime.UtcNow.AddDays(3));
        await AssignSlotAsync(client, guildId, branchId, eventId, characterId, groupNumber: 1, slotNumber: 1);

        var rangeStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var rangeEnd = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));
        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/raids/board?rangeStart={rangeStart:yyyy-MM-dd}&rangeEnd={rangeEnd:yyyy-MM-dd}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var events = json.GetProperty("events").EnumerateArray().ToList();
        var ev = events.Should().ContainSingle(e => e.GetProperty("id").GetInt32() == eventId).Subject;
        ev.GetProperty("raidZones").EnumerateArray().Should().ContainSingle(z => z.GetProperty("id").GetInt32() == KarazhanZoneId);
        ev.GetProperty("assignments").EnumerateArray().Should().ContainSingle(a => a.GetProperty("characterId").GetInt32() == characterId);
    }

    // ── Events (ad-hoc) ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateEvent_Persists_ReturnsCreatedIdAsDraftScheduled()
    {
        const string id = "610000000000000030";
        const string guildId = "630000000000000030";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);

        var eventId = await CreateAdhocEventAsync(client, guildId, branchId);

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var ev = await db.RaidEvents.FirstAsync(e => e.Id == eventId);
            ev.Status.Should().Be(RaidEventStatus.Scheduled);
            ev.PublicationStatus.Should().Be(RaidPublicationStatus.Draft);
        }
    }

    [Fact]
    public async Task UpdateEvent_UpdatesScalarFieldsAndZones()
    {
        const string id = "610000000000000031";
        const string guildId = "630000000000000031";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);
        var eventId = await CreateAdhocEventAsync(client, guildId, branchId);

        var response = await client.PatchAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/raids/events/{eventId}", JsonContent.Create(new
        {
            name = "Renamed event",
            startsAtUtc = DateTime.UtcNow.AddDays(5),
            groupCount = 3,
            slotsPerGroup = 5,
            raidZoneIds = GruulsLairZoneIds,
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var ev = await db.RaidEvents.Include(e => e.TargetZones).FirstAsync(e => e.Id == eventId);
            ev.Name.Should().Be("Renamed event");
            ev.GroupCount.Should().Be(3);
            ev.TargetZones.Should().ContainSingle(z => z.RaidZoneId == 2);
        }
    }

    [Fact]
    public async Task UpdateEvent_EventNotFound_Returns400()
    {
        const string id = "610000000000000131";
        const string guildId = "630000000000000131";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PatchAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/raids/events/999999", JsonContent.Create(new
        {
            name = "Renamed event",
            startsAtUtc = DateTime.UtcNow.AddDays(5),
            groupCount = 2,
            slotsPerGroup = 5,
            raidZoneIds = DefaultZoneIds,
        }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be("RaidEventNotFound");
    }

    [Fact]
    public async Task DeleteEvent_EventNotFound_Returns400()
    {
        const string id = "610000000000000132";
        const string guildId = "630000000000000132";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.DeleteAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/raids/events/999999");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be("RaidEventNotFound");
    }

    [Fact]
    public async Task DeleteEvent_RemovesEventAndCascadesAssignments()
    {
        const string id = "610000000000000032";
        const string guildId = "630000000000000032";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);
        var characterId = await SeedCharacterOnRoster(id, guildId, branchId, bnetCharacterId: 610032001);
        var eventId = await CreateAdhocEventAsync(client, guildId, branchId);
        await AssignSlotAsync(client, guildId, branchId, eventId, characterId, groupNumber: 1, slotNumber: 1);

        var response = await client.DeleteAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/raids/events/{eventId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            (await db.RaidEvents.AnyAsync(e => e.Id == eventId)).Should().BeFalse();
            (await db.RaidSlotAssignments.AnyAsync(a => a.RaidEventId == eventId)).Should().BeFalse();
        }
    }

    [Fact]
    public async Task PublishEvent_SetsPublishedStatusAndTimestamp()
    {
        const string id = "610000000000000033";
        const string guildId = "630000000000000033";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);
        var eventId = await CreateAdhocEventAsync(client, guildId, branchId);

        var response = await client.PostAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/raids/events/{eventId}/publish", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var ev = await db.RaidEvents.FirstAsync(e => e.Id == eventId);
            ev.PublicationStatus.Should().Be(RaidPublicationStatus.Published);
            ev.PublishedAt.Should().NotBeNull();
            ev.PublishedByDiscordId.Should().Be(id);
        }
    }

    [Fact]
    public async Task PublishEvent_AlreadyPublished_Returns400()
    {
        const string id = "610000000000000034";
        const string guildId = "630000000000000034";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);
        var eventId = await CreateAdhocEventAsync(client, guildId, branchId);
        await client.PostAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/raids/events/{eventId}/publish", null);

        var response = await client.PostAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/raids/events/{eventId}/publish", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be("RaidEventAlreadyPublished");
    }

    // ── Slot assignments ─────────────────────────────────────────────────────

    [Fact]
    public async Task AssignSlot_Success_PersistsAssignmentDefaultingToMainSpec()
    {
        const string id = "610000000000000040";
        const string guildId = "630000000000000040";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);
        var characterId = await SeedCharacterOnRoster(id, guildId, branchId, bnetCharacterId: 610040001, mainSpecId: 62);
        var eventId = await CreateAdhocEventAsync(client, guildId, branchId);

        var response = await AssignSlotAsync(client, guildId, branchId, eventId, characterId, groupNumber: 1, slotNumber: 1);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var assignment = await db.RaidSlotAssignments.FirstAsync(a => a.RaidEventId == eventId);
            assignment.CharacterId.Should().Be(characterId);
            assignment.SpecId.Should().Be(62);
            assignment.GroupNumber.Should().Be(1);
            assignment.SlotNumber.Should().Be(1);
        }
    }

    [Fact]
    public async Task AssignSlot_CharacterNotOnBranchRoster_Returns400()
    {
        const string id = "610000000000000041";
        const string guildId = "630000000000000041";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);
        var eventId = await CreateAdhocEventAsync(client, guildId, branchId);

        var response = await AssignSlotAsync(client, guildId, branchId, eventId, characterId: 999999, groupNumber: 1, slotNumber: 1);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be("CharacterNotOnRoster");
    }

    [Fact]
    public async Task AssignSlot_RepositioningSameCharacterWithinEvent_ReplacesRatherThanDuplicates()
    {
        const string id = "610000000000000042";
        const string guildId = "630000000000000042";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);
        var characterId = await SeedCharacterOnRoster(id, guildId, branchId, bnetCharacterId: 610042001);
        var eventId = await CreateAdhocEventAsync(client, guildId, branchId);
        await AssignSlotAsync(client, guildId, branchId, eventId, characterId, groupNumber: 1, slotNumber: 1);

        var response = await AssignSlotAsync(client, guildId, branchId, eventId, characterId, groupNumber: 1, slotNumber: 2);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var assignments = await db.RaidSlotAssignments.Where(a => a.RaidEventId == eventId).ToListAsync();
            assignments.Should().ContainSingle();
            assignments[0].SlotNumber.Should().Be(2);
        }
    }

    [Fact]
    public async Task AssignSlot_SlotAlreadyOccupiedByAnotherCharacter_Returns400()
    {
        const string id = "610000000000000043";
        const string guildId = "630000000000000043";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);
        var characterA = await SeedCharacterOnRoster(id, guildId, branchId, bnetCharacterId: 610043001, name: "CharA");
        var characterB = await SeedCharacterOnRoster(id, guildId, branchId, bnetCharacterId: 610043002, name: "CharB");
        var eventId = await CreateAdhocEventAsync(client, guildId, branchId);
        await AssignSlotAsync(client, guildId, branchId, eventId, characterA, groupNumber: 1, slotNumber: 1);

        var response = await AssignSlotAsync(client, guildId, branchId, eventId, characterB, groupNumber: 1, slotNumber: 1);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be("SlotOccupied");
    }

    [Fact]
    public async Task AssignSlot_SameCharacterAcrossTwoEventsSharingLockoutWindow_ReturnsLockoutConflict()
    {
        const string id = "610000000000000044";
        const string guildId = "630000000000000044";
        var branchId = await SeedGuildAndBranch(id, guildId, region: "eu");
        var client = CreateAuthenticatedClient(discordId: id);
        var characterId = await SeedCharacterOnRoster(id, guildId, branchId, bnetCharacterId: 610044001);

        // Two instants one second apart share the same weekly lockout window regardless of the
        // eu region's actual reset weekday/time, since resets always land at midnight-adjacent
        // hours far from 20:00:00-20:00:01.
        var eventA = await CreateAdhocEventAsync(client, guildId, branchId, startsAtUtc: new DateTime(2026, 2, 5, 20, 0, 0, DateTimeKind.Utc));
        var eventB = await CreateAdhocEventAsync(client, guildId, branchId, startsAtUtc: new DateTime(2026, 2, 5, 20, 0, 1, DateTimeKind.Utc));
        await AssignSlotAsync(client, guildId, branchId, eventA, characterId, groupNumber: 1, slotNumber: 1);

        var response = await AssignSlotAsync(client, guildId, branchId, eventB, characterId, groupNumber: 1, slotNumber: 1);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be("RaidLockoutConflict");
    }

    [Fact]
    public async Task SwapSlotAssignments_SwapsCoordinates()
    {
        const string id = "610000000000000045";
        const string guildId = "630000000000000045";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);
        const string secondPlayerId = "610000000000000145";
        await SeedAsync(db => { db.Users.Add(TestDataBuilder.CreateUser(secondPlayerId)); return Task.CompletedTask; });
        var characterA = await SeedCharacterOnRoster(id, guildId, branchId, bnetCharacterId: 610045001, name: "CharA");
        var characterB = await SeedCharacterOnRoster(secondPlayerId, guildId, branchId, bnetCharacterId: 610045002, name: "CharB");
        var eventId = await CreateAdhocEventAsync(client, guildId, branchId);
        await AssignSlotAsync(client, guildId, branchId, eventId, characterA, groupNumber: 1, slotNumber: 1);
        await AssignSlotAsync(client, guildId, branchId, eventId, characterB, groupNumber: 2, slotNumber: 1);

        var response = await client.PostAsJsonAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/raids/events/{eventId}/slots/swap", new
        {
            groupNumberA = 1, slotNumberA = 1, groupNumberB = 2, slotNumberB = 1,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var atOriginalA = await db.RaidSlotAssignments.SingleAsync(a => a.RaidEventId == eventId && a.GroupNumber == 1 && a.SlotNumber == 1);
            var atOriginalB = await db.RaidSlotAssignments.SingleAsync(a => a.RaidEventId == eventId && a.GroupNumber == 2 && a.SlotNumber == 1);
            atOriginalA.CharacterId.Should().Be(characterB);
            atOriginalB.CharacterId.Should().Be(characterA);
        }
    }

    [Fact]
    public async Task SwapSlotAssignments_OneSlotEmpty_Returns400AndDoesNotMove()
    {
        const string id = "610000000000000149";
        const string guildId = "630000000000000149";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);
        var characterA = await SeedCharacterOnRoster(id, guildId, branchId, bnetCharacterId: 610145001, name: "CharA");
        var eventId = await CreateAdhocEventAsync(client, guildId, branchId);
        await AssignSlotAsync(client, guildId, branchId, eventId, characterA, groupNumber: 1, slotNumber: 1);

        var response = await client.PostAsJsonAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/raids/events/{eventId}/slots/swap", new
        {
            groupNumberA = 1, slotNumberA = 1, groupNumberB = 2, slotNumberB = 1,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be("BothSlotsMustBeOccupiedToSwap");
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var stillAtOriginal = await db.RaidSlotAssignments.SingleAsync(a => a.RaidEventId == eventId);
            stillAtOriginal.GroupNumber.Should().Be(1);
            stillAtOriginal.SlotNumber.Should().Be(1);
        }
    }

    [Fact]
    public async Task UnassignSlot_RemovesAssignment()
    {
        const string id = "610000000000000046";
        const string guildId = "630000000000000046";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);
        var characterId = await SeedCharacterOnRoster(id, guildId, branchId, bnetCharacterId: 610046001);
        var eventId = await CreateAdhocEventAsync(client, guildId, branchId);
        await AssignSlotAsync(client, guildId, branchId, eventId, characterId, groupNumber: 1, slotNumber: 1);

        var response = await client.PostAsJsonAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/raids/events/{eventId}/slots/unassign", new
        {
            groupNumber = 1, slotNumber = 1,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            (await db.RaidSlotAssignments.AnyAsync(a => a.RaidEventId == eventId)).Should().BeFalse();
        }
    }

    [Fact]
    public async Task UpdateSlotAssignmentSpec_ChangesToDeclaredOffSpec()
    {
        const string id = "610000000000000047";
        const string guildId = "630000000000000047";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);
        var characterId = await SeedCharacterOnRoster(id, guildId, branchId, bnetCharacterId: 610047001, mainSpecId: 62);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            db.CharacterRaidSpecs.Add(new CharacterRaidSpec { CharacterId = characterId, SpecId = 63, IsMain = false });
            await db.SaveChangesAsync();
        }
        var eventId = await CreateAdhocEventAsync(client, guildId, branchId);
        await AssignSlotAsync(client, guildId, branchId, eventId, characterId, groupNumber: 1, slotNumber: 1);

        var response = await client.PatchAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/raids/events/{eventId}/slots/spec", JsonContent.Create(new
        {
            groupNumber = 1, slotNumber = 1, specId = 63,
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope2, db2) = CreateDbScope();
        using (scope2)
        {
            var assignment = await db2.RaidSlotAssignments.SingleAsync(a => a.RaidEventId == eventId);
            assignment.SpecId.Should().Be(63);
        }
    }

    [Fact]
    public async Task UpdateSlotAssignmentSpec_SpecNotDeclaredByCharacter_Returns400()
    {
        const string id = "610000000000000048";
        const string guildId = "630000000000000048";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);
        var characterId = await SeedCharacterOnRoster(id, guildId, branchId, bnetCharacterId: 610048001, mainSpecId: 62);
        var eventId = await CreateAdhocEventAsync(client, guildId, branchId);
        await AssignSlotAsync(client, guildId, branchId, eventId, characterId, groupNumber: 1, slotNumber: 1);

        var response = await client.PatchAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/raids/events/{eventId}/slots/spec", JsonContent.Create(new
        {
            groupNumber = 1, slotNumber = 1, specId = 64, // Frost — never declared for this character.
        }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be("SpecNotAvailableForCharacter");
    }

    // ── Unassigned members ───────────────────────────────────────────────────

    [Fact]
    public async Task GetUnassignedMembers_ReturnsRosterCharacterNotYetAssigned()
    {
        const string id = "610000000000000050";
        const string guildId = "630000000000000050";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);
        var assignedCharacterId = await SeedCharacterOnRoster(id, guildId, branchId, bnetCharacterId: 610050001, name: "Assigned");
        var unassignedCharacterId = await SeedCharacterOnRoster(id, guildId, branchId, bnetCharacterId: 610050002, name: "Unassigned");
        var eventId = await CreateAdhocEventAsync(client, guildId, branchId);
        await AssignSlotAsync(client, guildId, branchId, eventId, assignedCharacterId, groupNumber: 1, slotNumber: 1);
        await client.PostAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/raids/events/{eventId}/publish", null);

        var rangeStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var rangeEnd = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));
        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/raids/unassigned-members?rangeStart={rangeStart:yyyy-MM-dd}&rangeEnd={rangeEnd:yyyy-MM-dd}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var members = json.EnumerateArray().ToList();
        members.Should().ContainSingle(m => m.GetProperty("characterId").GetInt32() == unassignedCharacterId);
        members.Should().NotContain(m => m.GetProperty("characterId").GetInt32() == assignedCharacterId);
    }

    // ── Event summary ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetEventSummary_Success_ReturnsIdAndName()
    {
        const string id = "610000000000000060";
        const string guildId = "630000000000000060";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);
        var eventId = await CreateAdhocEventAsync(client, guildId, branchId);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/raids/events/{eventId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("id").GetInt32().Should().Be(eventId);
        json.GetProperty("name").GetString().Should().Be("One-shot event");
    }

    [Fact]
    public async Task GetEventSummary_EventNotFound_Returns400()
    {
        const string id = "610000000000000061";
        const string guildId = "630000000000000061";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/raids/events/999999");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be("RaidEventNotFound");
    }

    // ── Assigned characters ──────────────────────────────────────────────────

    [Fact]
    public async Task GetAssignedCharacters_WhenNotOfficer_Returns400()
    {
        const string id = "610000000000000062";
        const string guildId = "630000000000000062";
        var branchId = await SeedGuildAndBranch(id, guildId, isAdmin: false);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/raids/events/999999/assigned-characters");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be("Forbidden");
    }

    [Fact]
    public async Task GetAssignedCharacters_Success_ReturnsAssignedCharacter()
    {
        const string id = "610000000000000063";
        const string guildId = "630000000000000063";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);
        var characterId = await SeedCharacterOnRoster(id, guildId, branchId, bnetCharacterId: 610063001, name: "Assigned");
        var eventId = await CreateAdhocEventAsync(client, guildId, branchId);
        await AssignSlotAsync(client, guildId, branchId, eventId, characterId, groupNumber: 1, slotNumber: 1);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/raids/events/{eventId}/assigned-characters");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var characters = json.EnumerateArray().ToList();
        characters.Should().ContainSingle(c => c.GetProperty("characterId").GetInt32() == characterId && c.GetProperty("name").GetString() == "Assigned");
    }

    // ── Announce grouping ────────────────────────────────────────────────────

    [Fact]
    public async Task AnnounceGrouping_WhenNotOfficer_Returns400()
    {
        const string id = "610000000000000064";
        const string guildId = "630000000000000064";
        var branchId = await SeedGuildAndBranch(id, guildId, isAdmin: false);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PostAsJsonAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/raids/events/999999/announce-grouping", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be("Forbidden");
    }

    [Fact]
    public async Task AnnounceGrouping_PublishedEventWithNoAssignments_ReturnsNoAssignmentsToNotify()
    {
        const string id = "610000000000000065";
        const string guildId = "630000000000000065";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);
        var eventId = await CreateAdhocEventAsync(client, guildId, branchId);
        await client.PostAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/raids/events/{eventId}/publish", null);
        await SeedAsync(db =>
        {
            db.GuildNotificationSettings.Add(new GuildNotificationSetting
            {
                GuildId = guildId,
                EventType = GuildNotificationEventType.RaidCompositionAnnouncementPosted,
                GuildBranchId = branchId,
                Enabled = true,
                ChannelId = "123",
            });
            return Task.CompletedTask;
        });

        var response = await client.PostAsJsonAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/raids/events/{eventId}/announce-grouping", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be("NoAssignmentsToNotify");
    }

    // ── Seeding helpers ──────────────────────────────────────────────────────

    private async Task<int> SeedGuildAndBranch(string discordId, string guildId, bool isAdmin = true, string? region = null)
    {
        var branch = TestDataBuilder.CreateGuildBranch(guildId, branchId: RaidBranchId);
        if (region != null)
            branch.Region = region;

        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(discordId));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.GuildBranches.Add(branch);
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(discordId, guildId, isAdmin: isAdmin));
            return Task.CompletedTask;
        });

        return branch.Id;
    }

    private async Task<int> SeedCharacterOnRoster(string discordId, string guildId, int guildBranchId, long bnetCharacterId, int mainSpecId = 62, string name = "TestMage")
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaidOpsDbContext>();

        var realm = TestDataBuilder.CreateRealm(branchId: RaidBranchId, slug: $"realm-raid-{bnetCharacterId}");
        db.Realms.Add(realm);
        await db.SaveChangesAsync();

        var character = TestDataBuilder.CreateCharacter(discordId, realm.Id, branchId: RaidBranchId, isActive: true, bnetCharacterId: bnetCharacterId, name: name);
        db.Characters.Add(character);
        await db.SaveChangesAsync();

        db.CharacterRaidSpecs.Add(new CharacterRaidSpec { CharacterId = character.Id, SpecId = mainSpecId, IsMain = true });
        db.GuildMemberships.Add(new GuildMembership { CharacterId = character.Id, GuildId = guildId, GuildBranchId = guildBranchId, CharacterRank = CharacterRank.Main, JoinedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        return character.Id;
    }

    private static async Task<int> CreateSeriesAsync(HttpClient client, string guildId, int guildBranchId, DayOfWeek dayOfWeek = DayOfWeek.Wednesday, int[]? zoneIds = null)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/guilds/{guildId}/branches/{guildBranchId}/raids/series", new
        {
            name = "Split 1",
            recurrenceDayOfWeek = dayOfWeek,
            recurrenceStartTimeLocal = new TimeOnly(20, 0),
            recurrenceIntervalWeeks = 1,
            groupCount = 2,
            slotsPerGroup = 5,
            raidZoneIds = zoneIds ?? DefaultZoneIds,
        });
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("body").GetProperty("id").GetInt32();
    }

    private static async Task<int> CreateAdhocEventAsync(HttpClient client, string guildId, int guildBranchId, DateTime? startsAtUtc = null, int[]? zoneIds = null)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/guilds/{guildId}/branches/{guildBranchId}/raids/events", new
        {
            name = "One-shot event",
            startsAtUtc = startsAtUtc ?? DateTime.UtcNow.AddDays(3),
            groupCount = 2,
            slotsPerGroup = 5,
            raidZoneIds = zoneIds ?? DefaultZoneIds,
        });
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("body").GetProperty("id").GetInt32();
    }

    private static Task<HttpResponseMessage> AssignSlotAsync(HttpClient client, string guildId, int guildBranchId, int eventId, int characterId, int groupNumber, int slotNumber) =>
        client.PostAsJsonAsync($"/api/v1/guilds/{guildId}/branches/{guildBranchId}/raids/events/{eventId}/slots/assign", new
        {
            groupNumber, slotNumber, characterId,
        });

    private static DateOnly NextDateOnOrAfter(DateOnly date, DayOfWeek dayOfWeek)
    {
        while (date.DayOfWeek != dayOfWeek)
            date = date.AddDays(1);
        return date;
    }
}
