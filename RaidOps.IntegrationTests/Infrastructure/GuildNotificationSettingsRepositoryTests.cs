using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.IntegrationTests.Infrastructure;

[Collection("Integration")]
public class GuildNotificationSettingsRepositoryTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private async Task SeedGuildAsync(string guildId) =>
        await SeedAsync(db =>
        {
            db.Guilds.Add(new Guild { Id = guildId, Name = "Guild" });
            return Task.CompletedTask;
        });

    [Fact]
    public async Task GetAllForGuildAsync_NoRows_ReturnsEmpty()
    {
        const string guildId = "930000000000000001";
        await SeedGuildAsync(guildId);

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildNotificationSettingsRepository>();
            var result = await repo.GetAllForGuildAsync(guildId);

            result.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task GetAsync_NoRow_ReturnsNull()
    {
        const string guildId = "930000000000000002";
        await SeedGuildAsync(guildId);

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildNotificationSettingsRepository>();
            var result = await repo.GetAsync(guildId, GuildNotificationEventType.AbsenceAdded);

            result.Should().BeNull();
        }
    }

    [Fact]
    public async Task UpsertRangeAsync_NewRows_InsertsThem()
    {
        const string guildId = "930000000000000003";
        await SeedGuildAsync(guildId);

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildNotificationSettingsRepository>();
            await repo.UpsertRangeAsync(guildId,
            [
                new GuildNotificationSetting { GuildId = guildId, EventType = GuildNotificationEventType.AbsenceAdded, Enabled = true, ChannelId = "chan-1" },
                new GuildNotificationSetting { GuildId = guildId, EventType = GuildNotificationEventType.AbsenceRemoved, Enabled = false, ChannelId = null },
            ]);

            var rows = await db.GuildNotificationSettings.Where(s => s.GuildId == guildId).ToListAsync();
            rows.Should().HaveCount(2);
            rows.Should().ContainSingle(s => s.EventType == GuildNotificationEventType.AbsenceAdded && s.Enabled && s.ChannelId == "chan-1");
            rows.Should().ContainSingle(s => s.EventType == GuildNotificationEventType.AbsenceRemoved && !s.Enabled && s.ChannelId == null);
        }
    }

    [Fact]
    public async Task UpsertRangeAsync_ExistingRow_UpdatesInPlace()
    {
        const string guildId = "930000000000000004";
        await SeedGuildAsync(guildId);
        await SeedAsync(db =>
        {
            db.GuildNotificationSettings.Add(new GuildNotificationSetting
            {
                GuildId = guildId, EventType = GuildNotificationEventType.AbsenceAdded, Enabled = false, ChannelId = null,
            });
            return Task.CompletedTask;
        });

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildNotificationSettingsRepository>();
            await repo.UpsertRangeAsync(guildId,
            [
                new GuildNotificationSetting { GuildId = guildId, EventType = GuildNotificationEventType.AbsenceAdded, Enabled = true, ChannelId = "chan-2" },
            ]);

            var rows = await db.GuildNotificationSettings.Where(s => s.GuildId == guildId).ToListAsync();
            rows.Should().ContainSingle();
            rows[0].Enabled.Should().BeTrue();
            rows[0].ChannelId.Should().Be("chan-2");
        }
    }

    [Fact]
    public async Task GetAsync_AfterUpsert_ReturnsPersistedRow()
    {
        const string guildId = "930000000000000005";
        await SeedGuildAsync(guildId);
        await SeedAsync(db =>
        {
            db.GuildNotificationSettings.Add(new GuildNotificationSetting
            {
                GuildId = guildId, EventType = GuildNotificationEventType.AbsenceRemoved, Enabled = true, ChannelId = "chan-3",
            });
            return Task.CompletedTask;
        });

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildNotificationSettingsRepository>();
            var result = await repo.GetAsync(guildId, GuildNotificationEventType.AbsenceRemoved);

            result.Should().NotBeNull();
            result!.Enabled.Should().BeTrue();
            result.ChannelId.Should().Be("chan-3");
        }
    }
}
