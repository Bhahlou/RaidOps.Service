using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RaidOps.Application.Contracts.Raids.CompositionPreviews.Responses;
using RaidOps.Domain.Models.Raids.CompositionPreviews;
using RaidOps.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RaidOps.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for <see cref="RaidOps.API.Controllers.v1.RaidCompositionPreviewsController"/>.
/// All Discord IDs and guild IDs are in the 985… range to avoid primary-key conflicts with other
/// test classes (970… is already claimed by <see cref="GuildBranchesControllerTests"/> and
/// <see cref="GuildRosterControllerTests"/>). Warrior (class 1) / Arms (spec 71) are used as the valid class/spec pair
/// throughout — reference data, seeded regardless of branch/expansion.
/// </summary>
[Collection("Integration")]
public class RaidCompositionPreviewsControllerTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private const int WarriorClassId = 1;
    private const int PaladinClassId = 2;
    private const int ArmsSpecId = 71;

    // ── Auth enforcement ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetPreviews_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/guilds/985000000000000001/branches/1/composition-previews");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPreview_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/guilds/985000000000000001/branches/1/composition-previews/1");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreatePreview_WithoutToken_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/guilds/985000000000000001/branches/1/composition-previews", new { name = "x", groupCount = 8 });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RenamePreview_WithoutToken_Returns401()
    {
        var response = await Client.PatchAsync("/api/v1/guilds/985000000000000001/branches/1/composition-previews/1", JsonContent.Create(new { name = "x" }));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DuplicatePreview_WithoutToken_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/guilds/985000000000000001/branches/1/composition-previews/1/duplicate", new { newName = "x" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeletePreview_WithoutToken_Returns401()
    {
        var response = await Client.DeleteAsync("/api/v1/guilds/985000000000000001/branches/1/composition-previews/1");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateSlot_WithoutToken_Returns401()
    {
        var response = await Client.PatchAsync("/api/v1/guilds/985000000000000001/branches/1/composition-previews/1/slots", JsonContent.Create(new { groupNumber = 1, slotNumber = 1 }));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPreviews_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.GetAsync("/api/v1/guilds/985000000000000001/branches/1/composition-previews");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPreview_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.GetAsync("/api/v1/guilds/985000000000000001/branches/1/composition-previews/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreatePreview_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.PostAsJsonAsync("/api/v1/guilds/985000000000000001/branches/1/composition-previews", new { name = "x", groupCount = 8 });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RenamePreview_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.PatchAsync("/api/v1/guilds/985000000000000001/branches/1/composition-previews/1", JsonContent.Create(new { name = "x" }));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DuplicatePreview_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.PostAsJsonAsync("/api/v1/guilds/985000000000000001/branches/1/composition-previews/1/duplicate", new { newName = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeletePreview_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.DeleteAsync("/api/v1/guilds/985000000000000001/branches/1/composition-previews/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateSlot_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();

        var response = await client.PatchAsync("/api/v1/guilds/985000000000000001/branches/1/composition-previews/1/slots", JsonContent.Create(new { groupNumber = 1, slotNumber = 1 }));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GetPreviews ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPreviews_WhenNotOfficer_Returns400()
    {
        const string id = "985000000000000002";
        const string guildId = "985000000000000002";
        var branchId = await SeedGuildAndBranch(id, guildId, isAdmin: false);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/composition-previews");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetPreviews_WhenOfficer_ReturnsSeededSummaries()
    {
        const string id = "985000000000000003";
        const string guildId = "985000000000000003";
        var branchId = await SeedGuildAndBranch(id, guildId);
        await SeedPreview(branchId, id, "40-man target", groupCount: 8);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/composition-previews");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var previews = await response.Content.ReadFromJsonAsync<List<RaidCompositionPreviewSummaryResponse>>(ApiJsonOptions);
        previews.Should().ContainSingle(p => p.Name == "40-man target" && p.GroupCount == 8);
    }

    // ── GetPreview ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPreview_NotFound_Returns400()
    {
        const string id = "985000000000000004";
        const string guildId = "985000000000000004";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/composition-previews/999999");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetPreview_WhenOfficer_ReturnsPreviewWithSlots()
    {
        const string id = "985000000000000005";
        const string guildId = "985000000000000005";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var previewId = await SeedPreview(branchId, id, "40-man target", groupCount: 8);
        var client = CreateAuthenticatedClient(discordId: id);
        await client.PatchAsync(
            $"/api/v1/guilds/{guildId}/branches/{branchId}/composition-previews/{previewId}/slots",
            JsonContent.Create(new { groupNumber = 1, slotNumber = 1, wowClassId = WarriorClassId, specId = ArmsSpecId, note = "Bob" }));

        var response = await client.GetAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/composition-previews/{previewId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var preview = await response.Content.ReadFromJsonAsync<RaidCompositionPreviewResponse>(ApiJsonOptions);
        preview!.GroupCount.Should().Be(8);
        preview.SlotsPerGroup.Should().Be(5);
        var slot = preview.Slots.Should().ContainSingle().Which;
        slot.WowClassId.Should().Be(WarriorClassId);
        slot.WowClassColor.Should().Be("#C79C6E");
        slot.SpecId.Should().Be(ArmsSpecId);
        slot.Note.Should().Be("Bob");
    }

    // ── CreatePreview ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreatePreview_WhenOfficer_Returns200AndPersists()
    {
        const string id = "985000000000000006";
        const string guildId = "985000000000000006";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PostAsJsonAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/composition-previews", new { name = "New preview", groupCount = 4 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var created = await db.RaidCompositionPreviews.FirstOrDefaultAsync(p => p.GuildBranchId == branchId);
            created.Should().NotBeNull();
            created!.Name.Should().Be("New preview");
            created.GroupCount.Should().Be(4);
            created.SlotsPerGroup.Should().Be(5);
        }
    }

    [Fact]
    public async Task CreatePreview_GroupCountOutOfRange_Returns400WithInvalidGroupCountError()
    {
        const string id = "985000000000000007";
        const string guildId = "985000000000000007";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PostAsJsonAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/composition-previews", new { name = "Too big", groupCount = 9 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be("InvalidGroupCount");
    }

    [Fact]
    public async Task CreatePreview_WhenNotOfficer_Returns400()
    {
        const string id = "985000000000000008";
        const string guildId = "985000000000000008";
        var branchId = await SeedGuildAndBranch(id, guildId, isAdmin: false);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PostAsJsonAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/composition-previews", new { name = "x", groupCount = 8 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── RenamePreview ────────────────────────────────────────────────────────

    [Fact]
    public async Task RenamePreview_WhenOfficer_Returns200AndPersists()
    {
        const string id = "985000000000000009";
        const string guildId = "985000000000000009";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var previewId = await SeedPreview(branchId, id, "Old name", groupCount: 8);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PatchAsync(
            $"/api/v1/guilds/{guildId}/branches/{branchId}/composition-previews/{previewId}",
            JsonContent.Create(new { name = "New name" }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            (await db.RaidCompositionPreviews.FindAsync(previewId))!.Name.Should().Be("New name");
        }
    }

    [Fact]
    public async Task RenamePreview_NotFound_Returns400()
    {
        const string id = "985000000000000010";
        const string guildId = "985000000000000010";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PatchAsync(
            $"/api/v1/guilds/{guildId}/branches/{branchId}/composition-previews/999999",
            JsonContent.Create(new { name = "x" }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── DuplicatePreview ─────────────────────────────────────────────────────

    [Fact]
    public async Task DuplicatePreview_WhenOfficer_Returns200AndClonesSlots()
    {
        const string id = "985000000000000011";
        const string guildId = "985000000000000011";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var previewId = await SeedPreview(branchId, id, "Original", groupCount: 8);
        var client = CreateAuthenticatedClient(discordId: id);
        await client.PatchAsync(
            $"/api/v1/guilds/{guildId}/branches/{branchId}/composition-previews/{previewId}/slots",
            JsonContent.Create(new { groupNumber = 1, slotNumber = 1, wowClassId = WarriorClassId, specId = ArmsSpecId, note = "Bob" }));

        var response = await client.PostAsJsonAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/composition-previews/{previewId}/duplicate", new { newName = "Original (copy)" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var clone = await db.RaidCompositionPreviews.Include(p => p.Slots).FirstOrDefaultAsync(p => p.Name == "Original (copy)");
            clone.Should().NotBeNull();
            clone!.GroupCount.Should().Be(8);
            clone.Slots.Should().ContainSingle(s => s.WowClassId == WarriorClassId && s.SpecId == ArmsSpecId && s.Note == "Bob");
        }
    }

    [Fact]
    public async Task DuplicatePreview_SourceNotFound_Returns400()
    {
        const string id = "985000000000000012";
        const string guildId = "985000000000000012";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PostAsJsonAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/composition-previews/999999/duplicate", new { newName = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── DeletePreview ────────────────────────────────────────────────────────

    [Fact]
    public async Task DeletePreview_WhenOfficer_Returns200AndDeletes()
    {
        const string id = "985000000000000013";
        const string guildId = "985000000000000013";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var previewId = await SeedPreview(branchId, id, "To delete", groupCount: 8);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.DeleteAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/composition-previews/{previewId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            (await db.RaidCompositionPreviews.FindAsync(previewId)).Should().BeNull();
        }
    }

    [Fact]
    public async Task DeletePreview_NotFound_Returns400()
    {
        const string id = "985000000000000014";
        const string guildId = "985000000000000014";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.DeleteAsync($"/api/v1/guilds/{guildId}/branches/{branchId}/composition-previews/999999");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── UpdateSlot ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateSlot_WhenOfficer_Returns200AndPersistsPlaceholder()
    {
        const string id = "985000000000000015";
        const string guildId = "985000000000000015";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var previewId = await SeedPreview(branchId, id, "40-man", groupCount: 8);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PatchAsync(
            $"/api/v1/guilds/{guildId}/branches/{branchId}/composition-previews/{previewId}/slots",
            JsonContent.Create(new { groupNumber = 1, slotNumber = 1, wowClassId = WarriorClassId, specId = ArmsSpecId, note = (string?)null }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var slot = await db.RaidCompositionPreviewSlots.FirstOrDefaultAsync(s => s.RaidCompositionPreviewId == previewId);
            slot.Should().NotBeNull();
            slot!.WowClassId.Should().Be(WarriorClassId);
            slot.SpecId.Should().Be(ArmsSpecId);
        }
    }

    [Fact]
    public async Task UpdateSlot_ReplacingPlaceholder_KeepsExistingNote()
    {
        const string id = "985000000000000016";
        const string guildId = "985000000000000016";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var previewId = await SeedPreview(branchId, id, "40-man", groupCount: 8);
        var client = CreateAuthenticatedClient(discordId: id);
        await client.PatchAsync(
            $"/api/v1/guilds/{guildId}/branches/{branchId}/composition-previews/{previewId}/slots",
            JsonContent.Create(new { groupNumber = 1, slotNumber = 1, wowClassId = WarriorClassId, specId = ArmsSpecId, note = "Bob" }));

        var response = await client.PatchAsync(
            $"/api/v1/guilds/{guildId}/branches/{branchId}/composition-previews/{previewId}/slots",
            JsonContent.Create(new { groupNumber = 1, slotNumber = 1, wowClassId = PaladinClassId, specId = (int?)null, note = "Bob" }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var slot = await db.RaidCompositionPreviewSlots.SingleAsync(s => s.RaidCompositionPreviewId == previewId);
            slot.WowClassId.Should().Be(PaladinClassId);
            slot.SpecId.Should().BeNull();
            slot.Note.Should().Be("Bob");
        }
    }

    [Fact]
    public async Task UpdateSlot_ClearingEverything_DeletesTheSlotRow()
    {
        const string id = "985000000000000017";
        const string guildId = "985000000000000017";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var previewId = await SeedPreview(branchId, id, "40-man", groupCount: 8);
        var client = CreateAuthenticatedClient(discordId: id);
        await client.PatchAsync(
            $"/api/v1/guilds/{guildId}/branches/{branchId}/composition-previews/{previewId}/slots",
            JsonContent.Create(new { groupNumber = 1, slotNumber = 1, wowClassId = WarriorClassId, specId = ArmsSpecId, note = "Bob" }));

        var response = await client.PatchAsync(
            $"/api/v1/guilds/{guildId}/branches/{branchId}/composition-previews/{previewId}/slots",
            JsonContent.Create(new { groupNumber = 1, slotNumber = 1, wowClassId = (int?)null, specId = (int?)null, note = (string?)null }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            (await db.RaidCompositionPreviewSlots.FirstOrDefaultAsync(s => s.RaidCompositionPreviewId == previewId)).Should().BeNull();
        }
    }

    [Fact]
    public async Task UpdateSlot_OutOfBounds_Returns400WithInvalidGroupOrSlotNumberError()
    {
        const string id = "985000000000000018";
        const string guildId = "985000000000000018";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var previewId = await SeedPreview(branchId, id, "10-man", groupCount: 2);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PatchAsync(
            $"/api/v1/guilds/{guildId}/branches/{branchId}/composition-previews/{previewId}/slots",
            JsonContent.Create(new { groupNumber = 3, slotNumber = 1 }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be("InvalidGroupOrSlotNumber");
    }

    [Fact]
    public async Task UpdateSlot_SpecClassMismatch_Returns400WithSpecClassMismatchError()
    {
        const string id = "985000000000000019";
        const string guildId = "985000000000000019";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var previewId = await SeedPreview(branchId, id, "40-man", groupCount: 8);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PatchAsync(
            $"/api/v1/guilds/{guildId}/branches/{branchId}/composition-previews/{previewId}/slots",
            JsonContent.Create(new { groupNumber = 1, slotNumber = 1, wowClassId = PaladinClassId, specId = ArmsSpecId }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be("SpecClassMismatch");
    }

    [Fact]
    public async Task UpdateSlot_PreviewNotFound_Returns400()
    {
        const string id = "985000000000000020";
        const string guildId = "985000000000000020";
        var branchId = await SeedGuildAndBranch(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.PatchAsync(
            $"/api/v1/guilds/{guildId}/branches/{branchId}/composition-previews/999999/slots",
            JsonContent.Create(new { groupNumber = 1, slotNumber = 1 }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Seeding helpers ──────────────────────────────────────────────────────

    private async Task<int> SeedGuildAndBranch(string discordId, string guildId, bool isAdmin = true)
    {
        var branch = TestDataBuilder.CreateGuildBranch(guildId);
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

    private async Task<int> SeedPreview(int guildBranchId, string discordId, string name, int groupCount)
    {
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var preview = new RaidCompositionPreview
            {
                GuildBranchId = guildBranchId,
                Name = name,
                GroupCount = groupCount,
                SlotsPerGroup = 5,
                CreatedByDiscordId = discordId,
                CreatedAt = DateTime.UtcNow,
            };
            db.RaidCompositionPreviews.Add(preview);
            await db.SaveChangesAsync();
            return preview.Id;
        }
    }
}
