using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Calendar;
using RaidOps.Domain.Models.Discord;
using RaidOps.Infrastructure.Persistence.Implementations;
using RaidOps.Infrastructure.Persistence.Implementations.Repositories;
using RaidOps.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RaidOps.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for <see cref="RaidOps.API.Controllers.v1.AvailabilityController"/>.
/// Uses a shared Testcontainers PostgreSQL instance via <see cref="RaidOpsWebApplicationFactory"/>.
/// All Discord IDs are in the 980... range and guild IDs in the 950... range to avoid primary-key
/// conflicts with other test classes.
/// </summary>
[Collection("Integration")]
public class AvailabilityControllerTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    // ── Auth enforcement ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetAvailability_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync(
            $"/api/v1/me/availability?rangeStart={Today:yyyy-MM-dd}&rangeEnd={Today:yyyy-MM-dd}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateException_WithoutToken_Returns401()
    {
        var body = JsonContent.Create(new { startDate = Today, endDate = Today, status = "Available" });

        var response = await Client.PostAsync("/api/v1/me/availability/exceptions", body);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteException_WithoutToken_Returns401()
    {
        var response = await Client.DeleteAsync("/api/v1/me/availability/exceptions/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreatePattern_WithoutToken_Returns401()
    {
        var body = JsonContent.Create(new { cycleLengthDays = 7, anchorDate = Today, days = Array.Empty<object>() });

        var response = await Client.PostAsync("/api/v1/me/availability/patterns", body);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdatePattern_WithoutToken_Returns401()
    {
        var body = JsonContent.Create(new { cycleLengthDays = 7, anchorDate = Today, days = Array.Empty<object>() });

        var response = await Client.PatchAsync("/api/v1/me/availability/patterns/1", body);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeletePattern_WithoutToken_Returns401()
    {
        var response = await Client.DeleteAsync("/api/v1/me/availability/patterns/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateException_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();
        var body = JsonContent.Create(new { startDate = Today, endDate = Today, status = "Available" });

        var response = await client.PostAsync("/api/v1/me/availability/exceptions", body);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Roster access (branch-scoped declarations only — GetMyAvailability and a Global
    // declaration are purely self-scoped, with no guild access check at all) ────────────────────

    [Fact]
    public async Task CreateException_BranchScopedWhenNoRosterAccess_Returns400()
    {
        const string id      = "980000000000000002";
        const string guildId = "950000000000000002";
        GuildBranch? branch = null;
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(new Guild { Id = guildId, Name = "Test Guild", IsRegistered = true });
            branch = TestDataBuilder.CreateGuildBranch(guildId);
            db.GuildBranches.Add(branch);
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);
        var body = JsonContent.Create(new { guildId, guildBranchId = branch!.Id, startDate = Today, endDate = Today, status = "Available" });

        var response = await client.PostAsync("/api/v1/me/availability/exceptions", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be("Forbidden");
    }

    // ── GetAvailability — end-to-end resolution ─────────────────────────────

    [Fact]
    public async Task GetAvailability_ResolvesExceptionsAndPatterns_ReturnsExpectedDaysAndOnlyOpenPatternVersion()
    {
        const string id      = "980000000000000003";
        const string guildId = "950000000000000003";
        var guildBranchId = await SeedRosterAccess(id, guildId);
        await SeedAsync(db =>
        {
            db.AvailabilityExceptions.Add(new AvailabilityDeclaration
            {
                UserDiscordId = id,
                GuildId = guildId,
                GuildBranchId = guildBranchId,
                StartDate = Today,
                EndDate = Today,
                Status = DayAvailabilityStatus.Absent,
                Reason = "Vacation",
            });
            db.RecurringAvailabilityPatterns.Add(new RecurringAvailabilityPattern
            {
                UserDiscordId = id,
                GuildId = guildId,
                GuildBranchId = guildBranchId,
                Label = "Current",
                CycleLengthDays = 7,
                AnchorDate = Today,
                EffectiveFrom = Today,
                EffectiveUntil = null,
                Days =
                [
                    new RecurringAvailabilityPatternDay
                    {
                        OffsetInCycle = 2,
                        Status = DayAvailabilityStatus.Partial,
                        AvailableFrom = new TimeOnly(18, 0),
                        AvailableUntil = new TimeOnly(22, 0),
                    },
                ],
            });
            db.RecurringAvailabilityPatterns.Add(new RecurringAvailabilityPattern
            {
                UserDiscordId = id,
                GuildId = guildId,
                GuildBranchId = guildBranchId,
                Label = "Old",
                CycleLengthDays = 7,
                AnchorDate = Today.AddDays(-30),
                EffectiveFrom = Today.AddDays(-30),
                EffectiveUntil = Today.AddDays(-1),
            });
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);
        var rangeEnd = Today.AddDays(2);

        var response = await client.GetAsync(
            $"/api/v1/me/availability?rangeStart={Today:yyyy-MM-dd}&rangeEnd={rangeEnd:yyyy-MM-dd}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        var days = json.GetProperty("days");
        days.GetArrayLength().Should().Be(3);

        var day0 = days[0];
        day0.GetProperty("date").GetString().Should().Be(Today.ToString("yyyy-MM-dd"));
        day0.GetProperty("status").GetString().Should().Be("Absent");
        day0.GetProperty("isException").GetBoolean().Should().BeTrue();
        day0.GetProperty("reason").GetString().Should().Be("Vacation");

        var day1 = days[1];
        day1.GetProperty("status").GetString().Should().Be("Available");
        day1.GetProperty("isException").GetBoolean().Should().BeFalse();

        var day2 = days[2];
        day2.GetProperty("status").GetString().Should().Be("Partial");
        day2.GetProperty("isException").GetBoolean().Should().BeFalse();
        day2.GetProperty("availableFrom").GetString().Should().StartWith("18:00");
        day2.GetProperty("availableUntil").GetString().Should().StartWith("22:00");

        var patterns = json.GetProperty("patterns");
        patterns.GetArrayLength().Should().Be(1);
        patterns[0].GetProperty("label").GetString().Should().Be("Current");
    }

    // ── CreateException ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateException_StartDateInPast_Returns400PastDeclarationLocked()
    {
        const string id      = "980000000000000004";
        const string guildId = "950000000000000004";
        var guildBranchId = await SeedRosterAccess(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);
        var yesterday = Today.AddDays(-1);
        var body = JsonContent.Create(new { guildId, guildBranchId, startDate = yesterday, endDate = yesterday, status = "Absent" });

        var response = await client.PostAsync("/api/v1/me/availability/exceptions", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be("PastDeclarationLocked");
    }

    [Fact]
    public async Task CreateException_EndDateBeforeStartDate_Returns400InvalidRequest()
    {
        const string id      = "980000000000000005";
        const string guildId = "950000000000000005";
        var guildBranchId = await SeedRosterAccess(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);
        var tomorrow = Today.AddDays(1);
        var body = JsonContent.Create(new { guildId, guildBranchId, startDate = tomorrow, endDate = Today, status = "Absent" });

        var response = await client.PostAsync("/api/v1/me/availability/exceptions", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be("InvalidRequest");
    }

    [Fact]
    public async Task CreateException_ValidRange_Returns200AndPersists()
    {
        const string id      = "980000000000000006";
        const string guildId = "950000000000000006";
        var guildBranchId = await SeedRosterAccess(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);
        var tomorrow = Today.AddDays(1);

        var exceptionId = await CreateExceptionAsync(
            client, Today, tomorrow, "Partial", reason: "Doctor",
            availableFrom: new TimeOnly(9, 0), availableUntil: new TimeOnly(17, 0),
            guildId: guildId, guildBranchId: guildBranchId);

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var row = await db.AvailabilityExceptions.FirstAsync(e => e.Id == exceptionId);
            row.UserDiscordId.Should().Be(id);
            row.GuildId.Should().Be(guildId);
            row.GuildBranchId.Should().Be(guildBranchId);
            row.StartDate.Should().Be(Today);
            row.EndDate.Should().Be(tomorrow);
            row.Status.Should().Be(DayAvailabilityStatus.Partial);
            row.Reason.Should().Be("Doctor");
            row.AvailableFrom.Should().Be(new TimeOnly(9, 0));
            row.AvailableUntil.Should().Be(new TimeOnly(17, 0));
        }
    }

    [Fact]
    public async Task CreateException_PartialWithoutEitherBound_Returns400InvalidRequest()
    {
        const string id      = "980000000000000021";
        const string guildId = "950000000000000021";
        var guildBranchId = await SeedRosterAccess(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);
        var body = JsonContent.Create(new { guildId, guildBranchId, startDate = Today, endDate = Today, status = "Partial" });

        var response = await client.PostAsync("/api/v1/me/availability/exceptions", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be("InvalidRequest");
    }

    [Fact]
    public async Task CreateException_Global_Returns200AndPersistsWithNullScope()
    {
        const string id = "980000000000000023";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var exceptionId = await CreateExceptionAsync(client, Today, Today, "Absent");

        var (scope, db2) = CreateDbScope();
        using (scope)
        {
            var row = await db2.AvailabilityExceptions.FirstAsync(e => e.Id == exceptionId);
            row.GuildId.Should().BeNull();
            row.GuildBranchId.Should().BeNull();
        }
    }

    // ── DeleteException ──────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteException_AlreadyElapsed_Returns400AndRowStillExists()
    {
        const string id      = "980000000000000007";
        const string guildId = "950000000000000007";
        var guildBranchId = await SeedRosterAccess(id, guildId);
        AvailabilityDeclaration? seeded = null;
        await SeedAsync(db =>
        {
            seeded = new AvailabilityDeclaration
            {
                UserDiscordId = id,
                GuildId = guildId,
                GuildBranchId = guildBranchId,
                StartDate = Today.AddDays(-10),
                EndDate = Today.AddDays(-5),
                Status = DayAvailabilityStatus.Absent,
            };
            db.AvailabilityExceptions.Add(seeded);
            return Task.CompletedTask;
        });
        var exceptionId = seeded!.Id;
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.DeleteAsync($"/api/v1/me/availability/exceptions/{exceptionId}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be("PastDeclarationLocked");

        var (scope, db2) = CreateDbScope();
        using (scope)
        {
            var stillExists = await db2.AvailabilityExceptions.AnyAsync(e => e.Id == exceptionId);
            stillExists.Should().BeTrue();
        }
    }

    [Fact]
    public async Task DeleteException_NotOwner_Returns400AndRowStillExists()
    {
        const string ownerId    = "980000000000000008";
        const string attackerId = "980000000000000108";
        const string guildId    = "950000000000000008";
        var guildBranchId = await SeedRosterAccess(ownerId, guildId);
        await SeedAdditionalMember(attackerId, guildId);
        var ownerClient = CreateAuthenticatedClient(discordId: ownerId);
        var exceptionId = await CreateExceptionAsync(ownerClient, Today, Today.AddDays(1), "Absent", guildId: guildId, guildBranchId: guildBranchId);
        var attackerClient = CreateAuthenticatedClient(discordId: attackerId);

        var response = await attackerClient.DeleteAsync($"/api/v1/me/availability/exceptions/{exceptionId}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be("AvailabilityExceptionNotFound");

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var stillExists = await db.AvailabilityExceptions.AnyAsync(e => e.Id == exceptionId);
            stillExists.Should().BeTrue();
        }
    }

    [Fact]
    public async Task DeleteException_Valid_Returns200AndRemovesRow()
    {
        const string id      = "980000000000000009";
        const string guildId = "950000000000000009";
        var guildBranchId = await SeedRosterAccess(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);
        var exceptionId = await CreateExceptionAsync(client, Today, Today.AddDays(1), "Absent", guildId: guildId, guildBranchId: guildBranchId);

        var response = await client.DeleteAsync($"/api/v1/me/availability/exceptions/{exceptionId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var stillExists = await db.AvailabilityExceptions.AnyAsync(e => e.Id == exceptionId);
            stillExists.Should().BeFalse();
        }
    }

    // ── CreatePattern ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreatePattern_Valid_Returns200AndPersistsDays()
    {
        const string id      = "980000000000000010";
        const string guildId = "950000000000000010";
        var guildBranchId = await SeedRosterAccess(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);
        var days = new[] { Day(1, DayAvailabilityStatus.Absent, reason: "Raid night") };

        var patternId = await CreatePatternAsync(client, 7, Today, days, label: "Weekly", guildId: guildId, guildBranchId: guildBranchId);

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var pattern = await db.RecurringAvailabilityPatterns.Include(p => p.Days).FirstAsync(p => p.Id == patternId);
            pattern.Label.Should().Be("Weekly");
            pattern.CycleLengthDays.Should().Be(7);
            pattern.EffectiveFrom.Should().Be(Today);
            pattern.EffectiveUntil.Should().BeNull();
            pattern.Days.Should().ContainSingle(d =>
                d.OffsetInCycle == 1 && d.Status == DayAvailabilityStatus.Absent && d.Reason == "Raid night");
        }
    }

    [Fact]
    public async Task CreatePattern_PartialDayWithoutEitherBound_Returns400InvalidRequest()
    {
        const string id      = "980000000000000022";
        const string guildId = "950000000000000022";
        var guildBranchId = await SeedRosterAccess(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);
        var days = new[] { Day(0, DayAvailabilityStatus.Partial) };
        var body = JsonContent.Create(new { guildId, guildBranchId, cycleLengthDays = 7, anchorDate = Today, days });

        var response = await client.PostAsync("/api/v1/me/availability/patterns", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be("InvalidRequest");
    }

    // ── UpdatePattern ─────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdatePattern_SameDayEdit_HardDeletesOldRowAndCreatesNew()
    {
        const string id      = "980000000000000011";
        const string guildId = "950000000000000011";
        var guildBranchId = await SeedRosterAccess(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);
        var originalId = await CreatePatternAsync(
            client, 7, Today, [Day(1, DayAvailabilityStatus.Absent)], label: "Original", guildId: guildId, guildBranchId: guildBranchId);

        var newDays = new[] { Day(2, DayAvailabilityStatus.Partial, availableFrom: new TimeOnly(18, 0), availableUntil: new TimeOnly(22, 0)) };
        var body = JsonContent.Create(new { label = "Updated", cycleLengthDays = 5, anchorDate = Today, days = newDays });
        var response = await client.PatchAsync($"/api/v1/me/availability/patterns/{originalId}", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var newId = json.GetProperty("body").GetProperty("id").GetInt32();
        newId.Should().NotBe(originalId);

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var all = await db.RecurringAvailabilityPatterns
                .Where(p => p.UserDiscordId == id && p.GuildId == guildId)
                .Include(p => p.Days)
                .ToListAsync();
            all.Should().ContainSingle();
            var row = all[0];
            row.Id.Should().Be(newId);
            row.Label.Should().Be("Updated");
            row.CycleLengthDays.Should().Be(5);
            row.EffectiveFrom.Should().Be(Today);
            row.EffectiveUntil.Should().BeNull();
            row.Days.Should().ContainSingle(d => d.OffsetInCycle == 2 && d.Status == DayAvailabilityStatus.Partial);
        }
    }

    [Fact]
    public async Task UpdatePattern_PastEffectiveFrom_ClosesOldRowAndCreatesNew()
    {
        const string id      = "980000000000000012";
        const string guildId = "950000000000000012";
        var guildBranchId = await SeedRosterAccess(id, guildId);
        RecurringAvailabilityPattern? seeded = null;
        await SeedAsync(db =>
        {
            seeded = new RecurringAvailabilityPattern
            {
                UserDiscordId = id,
                GuildId = guildId,
                GuildBranchId = guildBranchId,
                Label = "Old",
                CycleLengthDays = 7,
                AnchorDate = Today.AddDays(-10),
                EffectiveFrom = Today.AddDays(-10),
                EffectiveUntil = null,
                Days = [new RecurringAvailabilityPatternDay { OffsetInCycle = 0, Status = DayAvailabilityStatus.Absent }],
            };
            db.RecurringAvailabilityPatterns.Add(seeded);
            return Task.CompletedTask;
        });
        var oldId = seeded!.Id;
        var client = CreateAuthenticatedClient(discordId: id);
        var newDays = new[] { Day(3, DayAvailabilityStatus.Absent) };
        var body = JsonContent.Create(new { label = "New", cycleLengthDays = 5, anchorDate = Today, days = newDays });

        var response = await client.PatchAsync($"/api/v1/me/availability/patterns/{oldId}", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var newId = json.GetProperty("body").GetProperty("id").GetInt32();

        var (scope, db2) = CreateDbScope();
        using (scope)
        {
            var oldRow = await db2.RecurringAvailabilityPatterns.FirstAsync(p => p.Id == oldId);
            oldRow.EffectiveUntil.Should().Be(Today.AddDays(-1));

            var newRow = await db2.RecurringAvailabilityPatterns.FirstAsync(p => p.Id == newId);
            newRow.EffectiveFrom.Should().Be(Today);
            newRow.EffectiveUntil.Should().BeNull();
            newRow.CycleLengthDays.Should().Be(5);
        }
    }

    [Fact]
    public async Task UpdatePattern_NotOwner_Returns400AndPatternUnchanged()
    {
        const string ownerId    = "980000000000000013";
        const string attackerId = "980000000000000113";
        const string guildId    = "950000000000000013";
        var guildBranchId = await SeedRosterAccess(ownerId, guildId);
        await SeedAdditionalMember(attackerId, guildId);
        var ownerClient = CreateAuthenticatedClient(discordId: ownerId);
        var patternId = await CreatePatternAsync(
            ownerClient, 7, Today, [Day(1, DayAvailabilityStatus.Absent)], label: "Owner Pattern", guildId: guildId, guildBranchId: guildBranchId);
        var attackerClient = CreateAuthenticatedClient(discordId: attackerId);
        var body = JsonContent.Create(new { label = "Hijacked", cycleLengthDays = 3, anchorDate = Today, days = new[] { Day(0, DayAvailabilityStatus.Absent) } });

        var response = await attackerClient.PatchAsync($"/api/v1/me/availability/patterns/{patternId}", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be("RecurringAvailabilityPatternNotFound");

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var row = await db.RecurringAvailabilityPatterns.FirstAsync(p => p.Id == patternId);
            row.Label.Should().Be("Owner Pattern");
            row.EffectiveUntil.Should().BeNull();
        }
    }

    // ── DeletePattern ─────────────────────────────────────────────────────────

    [Fact]
    public async Task DeletePattern_SameDayCreateThenDelete_HardDeletes()
    {
        const string id      = "980000000000000014";
        const string guildId = "950000000000000014";
        var guildBranchId = await SeedRosterAccess(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);
        var patternId = await CreatePatternAsync(
            client, 7, Today, [Day(1, DayAvailabilityStatus.Absent)], guildId: guildId, guildBranchId: guildBranchId);

        var response = await client.DeleteAsync($"/api/v1/me/availability/patterns/{patternId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var stillExists = await db.RecurringAvailabilityPatterns.AnyAsync(p => p.Id == patternId);
            stillExists.Should().BeFalse();
        }
    }

    [Fact]
    public async Task DeletePattern_PastEffectiveFrom_ClosesRow()
    {
        const string id      = "980000000000000015";
        const string guildId = "950000000000000015";
        var guildBranchId = await SeedRosterAccess(id, guildId);
        RecurringAvailabilityPattern? seeded = null;
        await SeedAsync(db =>
        {
            seeded = new RecurringAvailabilityPattern
            {
                UserDiscordId = id,
                GuildId = guildId,
                GuildBranchId = guildBranchId,
                Label = "Old",
                CycleLengthDays = 7,
                AnchorDate = Today.AddDays(-5),
                EffectiveFrom = Today.AddDays(-5),
                EffectiveUntil = null,
            };
            db.RecurringAvailabilityPatterns.Add(seeded);
            return Task.CompletedTask;
        });
        var patternId = seeded!.Id;
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.DeleteAsync($"/api/v1/me/availability/patterns/{patternId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db2) = CreateDbScope();
        using (scope)
        {
            var row = await db2.RecurringAvailabilityPatterns.FirstAsync(p => p.Id == patternId);
            row.EffectiveUntil.Should().Be(Today.AddDays(-1));
        }
    }

    [Fact]
    public async Task DeletePattern_NotOwner_Returns400AndPatternUnchanged()
    {
        const string ownerId    = "980000000000000016";
        const string attackerId = "980000000000000116";
        const string guildId    = "950000000000000016";
        var guildBranchId = await SeedRosterAccess(ownerId, guildId);
        await SeedAdditionalMember(attackerId, guildId);
        var ownerClient = CreateAuthenticatedClient(discordId: ownerId);
        var patternId = await CreatePatternAsync(
            ownerClient, 7, Today, [Day(1, DayAvailabilityStatus.Absent)], guildId: guildId, guildBranchId: guildBranchId);
        var attackerClient = CreateAuthenticatedClient(discordId: attackerId);

        var response = await attackerClient.DeleteAsync($"/api/v1/me/availability/patterns/{patternId}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be("RecurringAvailabilityPatternNotFound");

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var row = await db.RecurringAvailabilityPatterns.FirstAsync(p => p.Id == patternId);
            row.EffectiveUntil.Should().BeNull();
        }
    }

    // ── Audit log ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateException_BranchScoped_Success_WritesAuditLogEntry()
    {
        const string id      = "980000000000000017";
        const string guildId = "950000000000000017";
        var guildBranchId = await SeedRosterAccess(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);
        var tomorrow = Today.AddDays(1);

        await CreateExceptionAsync(client, Today, tomorrow, "Partial", reason: "Doctor",
            availableFrom: new TimeOnly(9, 0), availableUntil: new TimeOnly(17, 0),
            guildId: guildId, guildBranchId: guildBranchId);

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var log = await db.GuildAuditLogs.FirstOrDefaultAsync(l =>
                l.GuildId == guildId && l.ActionType == GuildAuditAction.AvailabilityExceptionDeclared);
            log.Should().NotBeNull();
            log!.ActorDiscordId.Should().Be(id);
            log.Details.Should().Contain("\"status\":\"Partial\"");
            log.Details.Should().Contain($"\"startDate\":\"{Today:yyyy-MM-dd}\"");
        }
    }

    /// <summary>
    /// The Global fan-out is the single most feature-defining new code path of the whole
    /// calendar-global-availability chantier — this proves it end to end against a real EF Core +
    /// Postgres write, not a mocked <c>IActiveRosterBranchResolver</c>/<c>IAuditLogService</c> like
    /// the unit tests already cover. Without an active roster character seeded on the branch, a
    /// Global mutation would resolve to zero branches and silently write no audit row at all.
    /// </summary>
    [Fact]
    public async Task CreateException_Global_WithActiveRosterCharacter_FansOutAuditLogToThatBranch()
    {
        const string id      = "980000000000000024";
        const string guildId = "950000000000000024";
        await SeedActiveRosterMember(id, guildId, bnetCharacterId: 99024);
        var client = CreateAuthenticatedClient(discordId: id);

        await CreateExceptionAsync(client, Today, Today, "Absent", reason: "Global absence");

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var log = await db.GuildAuditLogs.FirstOrDefaultAsync(l =>
                l.GuildId == guildId && l.ActionType == GuildAuditAction.AvailabilityExceptionDeclared);
            log.Should().NotBeNull();
            log!.ActorDiscordId.Should().Be(id);
        }
    }

    /// <summary>
    /// Regression test for a real bug hit in production: <c>GuildAuditLog.Details</c> was capped
    /// at 512 characters, which a shift rotation's full day-by-day JSON breakdown blew straight
    /// past (7 non-trivial days is already close to the old limit on its own). A mocked
    /// <c>IAuditLogService</c> unit test can never catch this — only a real write against Postgres can.
    /// </summary>
    [Fact]
    public async Task CreatePattern_ManyDaysDetail_WritesAuditLogEntryWithoutTruncation()
    {
        const string id      = "980000000000000018";
        const string guildId = "950000000000000018";
        var guildBranchId = await SeedRosterAccess(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);
        var days = Enumerable.Range(0, 7)
            .Select(i => Day(i, DayAvailabilityStatus.Partial, reason: $"Shift {i}", availableFrom: new TimeOnly(8, 0), availableUntil: new TimeOnly(20, 0)))
            .ToArray();

        await CreatePatternAsync(client, 7, Today, days, label: "Full rotation", guildId: guildId, guildBranchId: guildBranchId);

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var log = await db.GuildAuditLogs.FirstOrDefaultAsync(l =>
                l.GuildId == guildId && l.ActionType == GuildAuditAction.RecurringAvailabilityPatternCreated);
            log.Should().NotBeNull();
            // Details is a Dictionary<string,string> serialized to JSON — the "days" value is
            // itself a JSON-encoded string, so it needs a second parse to reach the actual array.
            var parsedDays = JsonDocument.Parse(JsonDocument.Parse(log.Details!).RootElement.GetProperty("days").GetString()!);
            parsedDays.RootElement.GetArrayLength().Should().Be(7);
            parsedDays.RootElement[6].GetProperty("offsetInCycle").GetInt32().Should().Be(6);
        }
    }

    // ── Cross-user isolation ──────────────────────────────────────────────────

    [Fact]
    public async Task GetAvailability_OnlyReturnsRequestersOwnExceptionsAndPatterns()
    {
        const string ownerId   = "980000000000000019";
        const string otherId   = "980000000000000119";
        const string guildId   = "950000000000000019";
        var guildBranchId = await SeedRosterAccess(ownerId, guildId);
        await SeedAdditionalMember(otherId, guildId);
        var ownerClient = CreateAuthenticatedClient(discordId: ownerId);
        var otherClient = CreateAuthenticatedClient(discordId: otherId);
        await CreateExceptionAsync(ownerClient, Today, Today, "Absent", reason: "Owner's declaration", guildId: guildId, guildBranchId: guildBranchId);

        var response = await otherClient.GetAsync(
            $"/api/v1/me/availability?rangeStart={Today:yyyy-MM-dd}&rangeEnd={Today:yyyy-MM-dd}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("exceptions").GetArrayLength().Should().Be(0);
        json.GetProperty("days")[0].GetProperty("status").GetString().Should().Be("Available");
    }

    // ── Repository — not-found branches ──────────────────────────────────────
    // The handlers' own pre-checks (existence, ownership) mean these repository-level "not found"
    // branches are only ever reached in a genuine concurrent-delete race in production — calling
    // the repository directly is the only realistic way to exercise them.

    [Fact]
    public async Task AvailabilityRepository_DeleteExceptionAsync_NonExistentId_ReturnsFalse()
    {
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var repository = new AvailabilityRepository(db);

            var deleted = await repository.DeleteExceptionAsync(999_999, "nonexistent-user", default);

            deleted.Should().BeFalse();
        }
    }

    [Fact]
    public async Task AvailabilityRepository_ClosePatternAsync_NonExistentId_ReturnsFalse()
    {
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var repository = new AvailabilityRepository(db);

            var closed = await repository.ClosePatternAsync(999_999, "nonexistent-user", Today, default);

            closed.Should().BeFalse();
        }
    }

    [Fact]
    public async Task AvailabilityRepository_DeletePatternAsync_NonExistentId_ReturnsFalse()
    {
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var repository = new AvailabilityRepository(db);

            var deleted = await repository.DeletePatternAsync(999_999, "nonexistent-user", default);

            deleted.Should().BeFalse();
        }
    }

    // ── Entity relationships (FK / navigation round-trip) ────────────────────

    [Fact]
    public async Task Entities_NavigationPropertiesAndForeignKeys_RoundTripCorrectly()
    {
        const string id      = "980000000000000020";
        const string guildId = "950000000000000020";
        var guildBranchId = await SeedRosterAccess(id, guildId);
        var client = CreateAuthenticatedClient(discordId: id);
        var exceptionId = await CreateExceptionAsync(client, Today, Today, "Absent", guildId: guildId, guildBranchId: guildBranchId);
        var patternId = await CreatePatternAsync(client, 7, Today, [Day(1, DayAvailabilityStatus.Absent)], guildId: guildId, guildBranchId: guildBranchId);

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var exception = await db.AvailabilityExceptions.Include(e => e.User).Include(e => e.Guild).Include(e => e.GuildBranch).FirstAsync(e => e.Id == exceptionId);
            exception.User.DiscordId.Should().Be(id);
            exception.Guild!.Id.Should().Be(guildId);
            exception.GuildBranch!.Id.Should().Be(guildBranchId);

            var pattern = await db.RecurringAvailabilityPatterns.Include(p => p.User).Include(p => p.Guild).FirstAsync(p => p.Id == patternId);
            pattern.User.DiscordId.Should().Be(id);
            pattern.Guild!.Id.Should().Be(guildId);

            var day = await db.RecurringAvailabilityPatternDays.Include(d => d.Pattern).FirstAsync(d => d.PatternId == patternId);
            day.Id.Should().BeGreaterThan(0);
            day.PatternId.Should().Be(patternId);
            day.Pattern.Id.Should().Be(patternId);
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds a user with plain Roster-level access to a registered guild's single active branch
    /// (no character/membership rows — a bare <see cref="Domain.Models.Discord.UserGuild"/> row is
    /// enough for every branch-scoped access check). Returns the branch's surrogate ID for building
    /// request bodies.
    /// </summary>
    private async Task<int> SeedRosterAccess(string discordId, string guildId)
    {
        GuildBranch? branch = null;
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(discordId));
            db.Guilds.Add(new Guild { Id = guildId, Name = "Test Guild", IsRegistered = true });
            branch = TestDataBuilder.CreateGuildBranch(guildId);
            db.GuildBranches.Add(branch);
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(discordId, guildId));
            return Task.CompletedTask;
        });
        return branch!.Id;
    }

    /// <summary>Adds a second member with Roster access to a guild already seeded by <see cref="SeedRosterAccess"/> — does not re-insert the guild row.</summary>
    private async Task SeedAdditionalMember(string discordId, string guildId)
    {
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(discordId));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(discordId, guildId));
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Seeds a user with an active roster character on a fresh registered guild/branch — the
    /// shape <see cref="RaidOps.Application.Contracts.Services.IActiveRosterBranchResolver"/> needs
    /// to fan a Global mutation out to that branch's audit log/notification.
    /// </summary>
    private async Task SeedActiveRosterMember(string discordId, string guildId, long bnetCharacterId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaidOpsDbContext>();

        db.Users.Add(TestDataBuilder.CreateUser(discordId));
        db.Guilds.Add(new Guild { Id = guildId, Name = "Test Guild", IsRegistered = true });
        var branch = TestDataBuilder.CreateGuildBranch(guildId);
        db.GuildBranches.Add(branch);
        await db.SaveChangesAsync();

        var realm = TestDataBuilder.CreateRealm(slug: $"realm-avail-{bnetCharacterId}");
        db.Realms.Add(realm);
        await db.SaveChangesAsync();

        var character = TestDataBuilder.CreateCharacter(discordId, realm.Id, isActive: true, bnetCharacterId: bnetCharacterId);
        db.Characters.Add(character);
        await db.SaveChangesAsync();

        db.GuildMemberships.Add(new GuildMembership
        {
            CharacterId = character.Id, GuildId = guildId, GuildBranch = branch,
            CharacterRank = CharacterRank.Main, JoinedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Creates a one-off availability exception via the API and returns its generated ID. Global
    /// by default (matching the API's own default) unless a branch scope is passed.
    /// </summary>
    private static async Task<int> CreateExceptionAsync(
        HttpClient client, DateOnly startDate, DateOnly endDate, string status,
        string? reason = null, TimeOnly? availableFrom = null, TimeOnly? availableUntil = null,
        string? guildId = null, int? guildBranchId = null)
    {
        var body = JsonContent.Create(new { guildId, guildBranchId, startDate, endDate, status, reason, availableFrom, availableUntil });
        var response = await client.PostAsync("/api/v1/me/availability/exceptions", body);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("body").GetProperty("id").GetInt32();
    }

    /// <summary>
    /// Creates a recurring availability pattern via the API and returns its generated ID. Global by
    /// default (matching the API's own default) unless a branch scope is passed.
    /// </summary>
    private static async Task<int> CreatePatternAsync(
        HttpClient client, int cycleLengthDays, DateOnly anchorDate,
        IEnumerable<object> days, string? label = null, string? guildId = null, int? guildBranchId = null)
    {
        var body = JsonContent.Create(new { guildId, guildBranchId, label, cycleLengthDays, anchorDate, days });
        var response = await client.PostAsync("/api/v1/me/availability/patterns", body);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("body").GetProperty("id").GetInt32();
    }

    /// <summary>
    /// Builds a <c>RecurringAvailabilityPatternDayInput</c>-shaped anonymous object for a pattern
    /// creation/update request body.
    /// </summary>
    private static object Day(int offsetInCycle, DayAvailabilityStatus status, string? reason = null, TimeOnly? availableFrom = null, TimeOnly? availableUntil = null)
        => new { offsetInCycle, status = status.ToString(), reason, availableFrom, availableUntil };
}
