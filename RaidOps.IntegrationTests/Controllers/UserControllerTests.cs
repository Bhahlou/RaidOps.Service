using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RaidOps.Application.Contracts.Authentication.Responses;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RaidOps.IntegrationTests.Controllers;

[Collection("Integration")]
public class UserControllerTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private const string Url = "/api/v1/user/me";
    private const string ChangelogSeenUrl = "/api/v1/user/changelog-seen";
    private const string DiscordId = "200000000000000001";

    private static readonly string[] SingleEntryId = ["e1"];
    private static readonly string[] TwoEntryIds = ["e1", "e2"];
    private static readonly string[] OverlappingEntryIds = ["e2", "e3"];

    // The API serializes enums as strings (see Program.cs's JsonStringEnumConverter registration)
    // but HttpContent.ReadFromJsonAsync defaults to numeric enum parsing unless told otherwise.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task GetMe_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync(Url);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMe_WhenUserNotInDb_Returns400()
    {
        var client = CreateAuthenticatedClient(discordId: "200000000000000099");

        var response = await client.GetAsync(Url);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetMe_WhenUserExists_ReturnsProfile()
    {
        await SeedAsync(db => { db.Users.Add(TestDataBuilder.CreateUser(DiscordId, "Bhahlou")); return Task.CompletedTask; });
        var client = CreateAuthenticatedClient(discordId: DiscordId, username: "Bhahlou");

        var response = await client.GetAsync(Url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        body!.DiscordId.Should().Be(DiscordId);
        body.Name.Should().Be("Bhahlou");
        body.Guilds.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMe_WhenUserHasRegisteredGuild_ReturnsGuildInResponse()
    {
        const string id = "200000000000000002";
        const string guildId = "850000000000000001";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: false));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync(Url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>(JsonOptions);
        body!.Guilds.Should().ContainSingle(g => g.Id == guildId && !g.IsAdmin && g.IsRegistered);
        body.Guilds.Single().AccessLevel.Should().Be(GuildAccessLevel.Public);
    }

    [Fact]
    public async Task GetMe_WhenUserIsAdminOfUnregisteredGuild_ReturnsGuildInResponse()
    {
        const string id = "200000000000000003";
        const string guildId = "850000000000000002";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: false));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync(Url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>(JsonOptions);
        body!.Guilds.Should().ContainSingle(g => g.Id == guildId && g.IsAdmin && !g.IsRegistered);
        body.Guilds.Single().AccessLevel.Should().Be(GuildAccessLevel.Officer);
    }

    // ── Notifications ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetMe_AdminOfGuildWithActiveBranchMissingOfficerRoles_ReturnsNotification()
    {
        const string id = "200000000000000004";
        const string guildId = "850000000000000003";
        await SeedAsync(db =>
        {
            var guild = TestDataBuilder.CreateGuild(guildId, isRegistered: true);
            guild.Timezone = "Europe/Paris";
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(guild);
            db.GuildBranches.Add(TestDataBuilder.CreateGuildBranch(guildId));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync(Url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>(JsonOptions);
        body!.Notifications.Should().ContainSingle(n =>
            n.Type == NotificationType.BranchOfficerRolesNotConfigured && n.GuildId == guildId);
    }

    [Fact]
    public async Task GetMe_AdminOfGuildWithBranchOfficerRolesConfigured_NoNotification()
    {
        const string id = "200000000000000005";
        const string guildId = "850000000000000004";
        await SeedAsync(db =>
        {
            var guild = TestDataBuilder.CreateGuild(guildId, isRegistered: true);
            guild.Timezone = "Europe/Paris";
            guild.Language = "en";
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(guild);
            var branch = TestDataBuilder.CreateGuildBranch(guildId, officerRoleIds: ["999000000000000001"]);
            branch.Region = "eu";
            db.GuildBranches.Add(branch);
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            // One saved row per notification family (Absence, Raid changes, Raid composition
            // changes) so this test stays focused on BranchOfficerRolesNotConfigured — without
            // these, the admin would also get nudged for the two other families having never
            // been configured either.
            db.GuildNotificationSettings.AddRange(
                new GuildNotificationSetting { GuildId = guildId, EventType = GuildNotificationEventType.AbsenceAdded, Enabled = false, ChannelId = null },
                new GuildNotificationSetting { GuildId = guildId, EventType = GuildNotificationEventType.RaidPublished, Enabled = false, ChannelId = null },
                new GuildNotificationSetting { GuildId = guildId, EventType = GuildNotificationEventType.RaidSlotAssigned, Enabled = false, ChannelId = null });
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync(Url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>(JsonOptions);
        body!.Notifications.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMe_AdminOfGuildStillOnboarding_NoNotification()
    {
        // Bot invited but no branch activated yet — never nudge mid get-started.
        const string id = "200000000000000006";
        const string guildId = "850000000000000005";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync(Url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>(JsonOptions);
        body!.Notifications.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMe_AdminDismissedNotification_NoNotification()
    {
        const string id = "200000000000000008";
        const string guildId = "850000000000000007";
        await SeedAsync(db =>
        {
            var guild = TestDataBuilder.CreateGuild(guildId, isRegistered: true);
            guild.Timezone = "Europe/Paris";
            guild.Language = "en";
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(guild);
            var branch = TestDataBuilder.CreateGuildBranch(guildId);
            branch.Region = "eu";
            db.GuildBranches.Add(branch);
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: true));
            // One saved row per notification family so only the explicitly-dismissed
            // BranchOfficerRolesNotConfigured nudge is at stake — see the comment on
            // GetMe_AdminOfGuildWithBranchOfficerRolesConfigured_NoNotification above.
            db.GuildNotificationSettings.AddRange(
                new GuildNotificationSetting { GuildId = guildId, EventType = GuildNotificationEventType.AbsenceAdded, Enabled = false, ChannelId = null },
                new GuildNotificationSetting { GuildId = guildId, EventType = GuildNotificationEventType.RaidPublished, Enabled = false, ChannelId = null },
                new GuildNotificationSetting { GuildId = guildId, EventType = GuildNotificationEventType.RaidSlotAssigned, Enabled = false, ChannelId = null });
            db.NotificationDismissals.Add(new NotificationDismissal
            {
                UserDiscordId = id,
                Type = NotificationType.BranchOfficerRolesNotConfigured,
                GuildId = guildId,
                DismissedAt = DateTime.UtcNow,
            });
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync(Url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>(JsonOptions);
        body!.Notifications.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMe_NonAdminOfConfiguredGuildWithoutOfficerThreshold_NoNotification()
    {
        const string id = "200000000000000007";
        const string guildId = "850000000000000006";
        await SeedAsync(db =>
        {
            var guild = TestDataBuilder.CreateGuild(guildId, isRegistered: true);
            guild.Timezone = "Europe/Paris";
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.Guilds.Add(guild);
            db.GuildBranches.Add(TestDataBuilder.CreateGuildBranch(guildId));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(id, guildId, isAdmin: false));
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync(Url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>(JsonOptions);
        body!.Notifications.Should().BeEmpty();
    }

    // ── Seen changelog entries ────────────────────────────────────────────

    [Fact]
    public async Task GetMe_UserHasSeenChangelogEntries_ReturnsThemInResponse()
    {
        const string id = "200000000000000009";
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(id));
            db.SeenChangelogEntries.Add(new SeenChangelogEntry { UserDiscordId = id, EntryId = "2026-08-02-raid-notifications", SeenAt = DateTime.UtcNow });
            return Task.CompletedTask;
        });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync(Url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        body!.SeenChangelogEntryIds.Should().ContainSingle().Which.Should().Be("2026-08-02-raid-notifications");
    }

    [Fact]
    public async Task GetMe_UserHasNoSeenChangelogEntries_ReturnsEmptyList()
    {
        const string id = "200000000000000013";
        await SeedAsync(db => { db.Users.Add(TestDataBuilder.CreateUser(id)); return Task.CompletedTask; });
        var client = CreateAuthenticatedClient(discordId: id);

        var response = await client.GetAsync(Url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        body!.SeenChangelogEntryIds.Should().BeEmpty();
    }

    // ── MarkChangelogSeen ──────────────────────────────────────────────────

    [Fact]
    public async Task MarkChangelogSeen_WithoutToken_Returns401()
    {
        var body = JsonContent.Create(new { entryIds = SingleEntryId });

        var response = await Client.PostAsync(ChangelogSeenUrl, body);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MarkChangelogSeen_TokenWithoutSubClaim_Returns401()
    {
        var client = CreateClientWithoutSubClaim();
        var body = JsonContent.Create(new { entryIds = SingleEntryId });

        var response = await client.PostAsync(ChangelogSeenUrl, body);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MarkChangelogSeen_Success_Returns200AndPersistsEntries()
    {
        const string id = "200000000000000010";
        await SeedAsync(db => { db.Users.Add(TestDataBuilder.CreateUser(id)); return Task.CompletedTask; });
        var client = CreateAuthenticatedClient(discordId: id);
        var body = JsonContent.Create(new { entryIds = TwoEntryIds });

        var response = await client.PostAsync(ChangelogSeenUrl, body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db2) = CreateDbScope();
        using (scope)
        {
            var seenIds = await db2.SeenChangelogEntries.Where(s => s.UserDiscordId == id).Select(s => s.EntryId).ToListAsync();
            seenIds.Should().BeEquivalentTo(TwoEntryIds);
        }
    }

    [Fact]
    public async Task MarkChangelogSeen_OverlappingEntries_DoesNotDuplicateAlreadySeenEntries()
    {
        const string id = "200000000000000011";
        await SeedAsync(db => { db.Users.Add(TestDataBuilder.CreateUser(id)); return Task.CompletedTask; });
        var client = CreateAuthenticatedClient(discordId: id);

        var first = await client.PostAsync(ChangelogSeenUrl, JsonContent.Create(new { entryIds = TwoEntryIds }));
        var second = await client.PostAsync(ChangelogSeenUrl, JsonContent.Create(new { entryIds = OverlappingEntryIds }));

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var seenIds = await db.SeenChangelogEntries.Where(s => s.UserDiscordId == id).Select(s => s.EntryId).ToListAsync();
            seenIds.Should().BeEquivalentTo(["e1", "e2", "e3"]);
        }
    }

    [Fact]
    public async Task MarkChangelogSeen_EmptyEntryIds_Returns200AndPersistsNothing()
    {
        const string id = "200000000000000012";
        await SeedAsync(db => { db.Users.Add(TestDataBuilder.CreateUser(id)); return Task.CompletedTask; });
        var client = CreateAuthenticatedClient(discordId: id);
        var body = JsonContent.Create(new { entryIds = Array.Empty<string>() });

        var response = await client.PostAsync(ChangelogSeenUrl, body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db2) = CreateDbScope();
        using (scope)
        {
            var count = await db2.SeenChangelogEntries.CountAsync(s => s.UserDiscordId == id);
            count.Should().Be(0);
        }
    }
}
