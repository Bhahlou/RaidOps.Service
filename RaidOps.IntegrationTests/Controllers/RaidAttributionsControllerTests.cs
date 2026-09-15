using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RaidOps.Application.Contracts.Raids.Attributions.Responses;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Raids;
using RaidOps.Domain.Models.Raids.Attributions;
using RaidOps.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RaidOps.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for <see cref="RaidOps.API.Controllers.v1.RaidAttributionsController"/>.
/// All Discord IDs and guild IDs are in the 981… range to avoid primary-key conflicts with other
/// test classes.
/// </summary>
[Collection("Integration")]
public class RaidAttributionsControllerTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    /// <summary>Seeds a guild + branch + one character seated (assigned) in a fresh raid event, and the guild's one attribution definition (a single name-slot cell, unrestricted unless overridden). Returns every ID the caller needs to build requests/assertions.</summary>
    private async Task<(int GuildBranchId, int EventId, int DefinitionId, int CellId, int CharacterId)> SeedRaidWithSeatedCharacterAsync(
        string discordId, string guildId, bool isOfficer, RaidPublicationStatus publicationStatus = RaidPublicationStatus.Published,
        List<int>? requiredClassIds = null, List<SpecRole>? requiredRoles = null, List<int>? requiredSpecIds = null, int seatedSpecId = 71, int seatedClassId = 1,
        RosterMode rosterMode = RosterMode.Open)
    {
        int guildBranchId, eventId, definitionId, cellId, characterId;

        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(discordId));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(discordId, guildId, isAdmin: isOfficer));
            return Task.CompletedTask;
        });

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            // RosterMode.Open (the default) grants Roster access to any member unconditionally —
            // DiscordRoleOnly (with a role this test's fake Discord bot presence never resolves)
            // is how a "not on roster" scenario is actually reachable here.
            var guildBranch = TestDataBuilder.CreateGuildBranch(guildId, branchId: 1, rosterMode: rosterMode, rosterRoleIds: rosterMode == RosterMode.DiscordRoleOnly ? ["roster-role"] : null);
            db.GuildBranches.Add(guildBranch);
            await db.SaveChangesAsync();
            guildBranchId = guildBranch.Id;

            var realm = TestDataBuilder.CreateRealm(branchId: 1, slug: $"realm-{guildId}");
            db.Realms.Add(realm);
            await db.SaveChangesAsync();

            var character = TestDataBuilder.CreateCharacter(discordId, realm.Id, branchId: 1, classId: seatedClassId, isActive: true, bnetCharacterId: long.Parse(guildId), name: "SeatedChar");
            db.Characters.Add(character);
            await db.SaveChangesAsync();
            characterId = character.Id;

            var raidEvent = new RaidEvent
            {
                GuildId = guildId,
                GuildBranchId = guildBranchId,
                Name = "Attributions Test Event",
                PublicationStatus = publicationStatus,
                StartsAtUtc = DateTime.UtcNow.AddDays(1),
                GroupCount = 2,
                SlotsPerGroup = 5,
                CreatedByDiscordId = discordId,
                CreatedAt = DateTime.UtcNow,
            };
            db.RaidEvents.Add(raidEvent);
            await db.SaveChangesAsync();
            eventId = raidEvent.Id;

            db.RaidSlotAssignments.Add(new RaidSlotAssignment
            {
                RaidEventId = eventId,
                GroupNumber = 1,
                SlotNumber = 1,
                CharacterId = characterId,
                SpecId = seatedSpecId,
                AssignedPlayerDiscordId = discordId,
                AssignedAt = DateTime.UtcNow,
                AssignedByDiscordId = discordId,
            });

            var definition = new GuildAttributionDefinition
            {
                GuildId = guildId,
                Label = "Tank",
                SortOrder = 0,
                CreatedAt = DateTime.UtcNow,
                CreatedByDiscordId = discordId,
                Cells =
                [
                    new AttributionDefinitionCell
                    {
                        CellIndex = 0,
                        Kind = AttributionCellKind.NameSlot,
                        SlotLabel = "Tank",
                        RequiredClassIds = requiredClassIds ?? [],
                        RequiredRoles = requiredRoles ?? [],
                        RequiredSpecIds = requiredSpecIds ?? [],
                    },
                ],
            };
            db.GuildAttributionDefinitions.Add(definition);
            await db.SaveChangesAsync();
            definitionId = definition.Id;
            cellId = definition.Cells.Single().Id;
        }

        return (guildBranchId, eventId, definitionId, cellId, characterId);
    }

    // ── Auth enforcement ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetAttributions_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/guilds/981000000000000001/branches/1/raids/events/1/attributions");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SetAttribution_WithoutToken_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/guilds/981000000000000001/branches/1/raids/events/1/attributions/set", new { definitionId = 1, cellId = 1, characterId = 1 });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ClearAttribution_WithoutToken_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/guilds/981000000000000001/branches/1/raids/events/1/attributions/clear", new { definitionId = 1, cellId = 1 });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAttributions_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.GetAsync("/api/v1/guilds/981000000000000001/branches/1/raids/events/1/attributions");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GetAttributions ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetAttributions_WhenNotOnRoster_Returns400()
    {
        const string id = "981000000000000002";
        const string guildId = "981000000000000002";
        var (guildBranchId, eventId, _, _, _) = await SeedRaidWithSeatedCharacterAsync(id, guildId, isOfficer: false, rosterMode: RosterMode.DiscordRoleOnly);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/branches/{guildBranchId}/raids/events/{eventId}/attributions");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAttributions_WhenOfficer_ReturnsDefinitionsAndSeatedCharacters()
    {
        const string id = "981000000000000003";
        const string guildId = "981000000000000003";
        var (guildBranchId, eventId, definitionId, cellId, characterId) = await SeedRaidWithSeatedCharacterAsync(id, guildId, isOfficer: true);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/branches/{guildBranchId}/raids/events/{eventId}/attributions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<RaidEventAttributionsResponse>(ApiJsonOptions);
        body!.Definitions.Should().ContainSingle(d => d.Id == definitionId && d.Cells.Any(c => c.Id == cellId));
        body.SeatedCharacters.Should().ContainSingle(c => c.CharacterId == characterId && c.Name == "SeatedChar");
        body.Fills.Should().BeEmpty();
    }

    // ── SetAttribution ───────────────────────────────────────────────────────

    [Fact]
    public async Task SetAttribution_WhenOfficerAndCharacterSeated_Returns200AndPersists()
    {
        const string id = "981000000000000005";
        const string guildId = "981000000000000005";
        var (guildBranchId, eventId, definitionId, cellId, characterId) = await SeedRaidWithSeatedCharacterAsync(id, guildId, isOfficer: true);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/guilds/{guildId}/branches/{guildBranchId}/raids/events/{eventId}/attributions/set",
            new { definitionId, cellId, instanceIndex = 0, characterId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var fill = await db.RaidEventAttributions.FirstOrDefaultAsync(f => f.RaidEventId == eventId && f.AttributionDefinitionCellId == cellId);
            fill.Should().NotBeNull();
            fill!.CharacterId.Should().Be(characterId);
        }
    }

    [Fact]
    public async Task SetAttribution_CharacterNotSeated_Returns400WithCharacterNotSeatedInEventError()
    {
        const string id = "981000000000000006";
        const string guildId = "981000000000000006";
        var (guildBranchId, eventId, definitionId, cellId, _) = await SeedRaidWithSeatedCharacterAsync(id, guildId, isOfficer: true);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/guilds/{guildId}/branches/{guildBranchId}/raids/events/{eventId}/attributions/set",
            new { definitionId, cellId, instanceIndex = 0, characterId = 999999 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be("CharacterNotSeatedInEvent");
    }

    [Fact]
    public async Task SetAttribution_ClassRestrictionNotMet_Returns400WithCharacterDoesNotMeetSlotRequirementError()
    {
        const string id = "981000000000000007";
        const string guildId = "981000000000000007";
        // Seated character is ClassId=1 (Warrior); the slot requires ClassId=9 (Warlock).
        var (guildBranchId, eventId, definitionId, cellId, characterId) = await SeedRaidWithSeatedCharacterAsync(id, guildId, isOfficer: true, requiredClassIds: [9], seatedClassId: 1);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/guilds/{guildId}/branches/{guildBranchId}/raids/events/{eventId}/attributions/set",
            new { definitionId, cellId, instanceIndex = 0, characterId });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be("CharacterDoesNotMeetSlotRequirement");
    }

    [Fact]
    public async Task SetAttribution_WhenNotOfficer_Returns400()
    {
        const string id = "981000000000000008";
        const string guildId = "981000000000000008";
        var (guildBranchId, eventId, definitionId, cellId, characterId) = await SeedRaidWithSeatedCharacterAsync(id, guildId, isOfficer: false);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/guilds/{guildId}/branches/{guildBranchId}/raids/events/{eventId}/attributions/set",
            new { definitionId, cellId, instanceIndex = 0, characterId });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── ClearAttribution ─────────────────────────────────────────────────────

    [Fact]
    public async Task ClearAttribution_WhenFilled_Returns200AndClears()
    {
        const string id = "981000000000000009";
        const string guildId = "981000000000000009";
        var (guildBranchId, eventId, definitionId, cellId, characterId) = await SeedRaidWithSeatedCharacterAsync(id, guildId, isOfficer: true);
        var (seedScope, seedDb) = CreateDbScope();
        using (seedScope)
        {
            seedDb.RaidEventAttributions.Add(new RaidEventAttribution
            {
                RaidEventId = eventId, AttributionDefinitionCellId = cellId, InstanceIndex = 0, GuildAttributionDefinitionId = definitionId,
                CharacterId = characterId, AssignedAt = DateTime.UtcNow, AssignedByDiscordId = id,
            });
            await seedDb.SaveChangesAsync();
        }
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/guilds/{guildId}/branches/{guildBranchId}/raids/events/{eventId}/attributions/clear",
            new { definitionId, cellId, instanceIndex = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            (await db.RaidEventAttributions.FirstOrDefaultAsync(f => f.RaidEventId == eventId && f.AttributionDefinitionCellId == cellId)).Should().BeNull();
        }
    }

    [Fact]
    public async Task ClearAttribution_AlreadyEmpty_Returns400WithSlotEmptyError()
    {
        const string id = "981000000000000010";
        const string guildId = "981000000000000010";
        var (guildBranchId, eventId, definitionId, cellId, _) = await SeedRaidWithSeatedCharacterAsync(id, guildId, isOfficer: true);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/guilds/{guildId}/branches/{guildBranchId}/raids/events/{eventId}/attributions/clear",
            new { definitionId, cellId, instanceIndex = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be("SlotEmpty");
    }
}
