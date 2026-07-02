using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Enums;
using RaidOps.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;

namespace RaidOps.IntegrationTests.Controllers;

[Collection("Integration")]
public class NotificationsControllerTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Dismiss_WithoutToken_Returns401()
    {
        var body = JsonContent.Create(new { type = "OfficerThresholdNotConfigured", guildId = "910000000000000020" });

        var response = await Client.PostAsync("/api/v1/notifications/dismiss", body);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Dismiss_Success_Returns200AndPersistsDismissal()
    {
        const string id      = "510000000000000020";
        const string guildId = "910000000000000020";
        await SeedAsync(db => { db.Users.Add(TestDataBuilder.CreateUser(id)); return Task.CompletedTask; });
        var client = CreateAuthenticatedClient(discordId: id);
        var body = JsonContent.Create(new { type = "OfficerThresholdNotConfigured", guildId });

        var response = await client.PostAsync("/api/v1/notifications/dismiss", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var dismissal = await db.NotificationDismissals.FirstOrDefaultAsync(d =>
                d.UserDiscordId == id && d.Type == NotificationType.OfficerThresholdNotConfigured && d.GuildId == guildId);
            dismissal.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task Dismiss_CalledTwice_IsIdempotent()
    {
        const string id      = "510000000000000021";
        const string guildId = "910000000000000021";
        await SeedAsync(db => { db.Users.Add(TestDataBuilder.CreateUser(id)); return Task.CompletedTask; });
        var client = CreateAuthenticatedClient(discordId: id);
        var body = JsonContent.Create(new { type = "OfficerThresholdNotConfigured", guildId });

        var first = await client.PostAsync("/api/v1/notifications/dismiss", body);
        var second = await client.PostAsync("/api/v1/notifications/dismiss", body);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var count = await db.NotificationDismissals.CountAsync(d => d.UserDiscordId == id && d.GuildId == guildId);
            count.Should().Be(1);
        }
    }
}
