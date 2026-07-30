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
            var result = await repo.GetAsync(guildId, GuildNotificationEventType.AbsenceAdded, null);

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
            await repo.UpsertRangeAsync(guildId, null,
            [
                new GuildNotificationSetting { GuildId = guildId, EventType = GuildNotificationEventType.AbsenceAdded, Enabled = true, ChannelId = "chan-1" },
                new GuildNotificationSetting { GuildId = guildId, EventType = GuildNotificationEventType.AbsenceRemoved, Enabled = false, ChannelId = null },
            ]);

            var rows = await db.GuildNotificationSettings.Where(s => s.GuildId == guildId).ToListAsync();
            rows.Should().HaveCount(2);
            rows.Should().ContainSingle(s => s.EventType == GuildNotificationEventType.AbsenceAdded && s.Enabled && s.ChannelId == "chan-1");
            rows.Should().ContainSingle(s => s.EventType == GuildNotificationEventType.AbsenceRemoved && !s.Enabled && s.ChannelId == null);
            rows.Should().OnlyContain(s => s.Id > 0, "each row should have been assigned a generated surrogate ID");
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
            await repo.UpsertRangeAsync(guildId, null,
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
            var result = await repo.GetAsync(guildId, GuildNotificationEventType.AbsenceRemoved, null);

            result.Should().NotBeNull();
            result!.Enabled.Should().BeTrue();
            result.ChannelId.Should().Be("chan-3");
        }
    }

    [Fact]
    public async Task GetAsync_BranchOverrideExists_ReturnsBranchRowNotGuildWideFallback()
    {
        const string guildId = "930000000000000009";
        var guildBranchId = await SeedGuildWithBranchAsync(guildId);
        await SeedAsync(db =>
        {
            db.GuildNotificationSettings.Add(new GuildNotificationSetting
            {
                GuildId = guildId, GuildBranchId = null, EventType = GuildNotificationEventType.AbsenceAdded, Enabled = true, ChannelId = "chan-guild-wide",
            });
            db.GuildNotificationSettings.Add(new GuildNotificationSetting
            {
                GuildId = guildId, GuildBranchId = guildBranchId, EventType = GuildNotificationEventType.AbsenceAdded, Enabled = true, ChannelId = "chan-branch-override",
            });
            return Task.CompletedTask;
        });

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildNotificationSettingsRepository>();
            var result = await repo.GetAsync(guildId, GuildNotificationEventType.AbsenceAdded, guildBranchId);

            result.Should().NotBeNull();
            result!.ChannelId.Should().Be("chan-branch-override");
        }
    }

    // ── GetEffectiveForGuildAsync ────────────────────────────────────────────

    /// <summary>
    /// Seeds a guild plus one active <see cref="GuildBranch"/> for it (the FK that
    /// <see cref="GuildNotificationSetting.GuildBranchId"/> targets), returning the branch's
    /// EF-generated surrogate ID.
    /// </summary>
    private async Task<int> SeedGuildWithBranchAsync(string guildId)
    {
        await SeedGuildAsync(guildId);
        var branch = TestDataBuilder.CreateGuildBranch(guildId);
        await SeedAsync(db =>
        {
            db.GuildBranches.Add(branch);
            return Task.CompletedTask;
        });
        return branch.Id;
    }

    [Fact]
    public async Task GetEffectiveForGuildAsync_OnlyGuildWideRow_ReturnsGuildWideRow()
    {
        const string guildId = "930000000000000006";
        var guildBranchId = await SeedGuildWithBranchAsync(guildId);
        await SeedAsync(db =>
        {
            db.GuildNotificationSettings.Add(new GuildNotificationSetting
            {
                GuildId = guildId, GuildBranchId = null, EventType = GuildNotificationEventType.AbsenceAdded, Enabled = true, ChannelId = "chan-guild-wide",
            });
            return Task.CompletedTask;
        });

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildNotificationSettingsRepository>();
            var result = await repo.GetEffectiveForGuildAsync(guildId, guildBranchId);

            result.Should().ContainSingle(s => s.EventType == GuildNotificationEventType.AbsenceAdded
                && s.GuildBranchId == null && s.ChannelId == "chan-guild-wide");
        }
    }

    [Fact]
    public async Task GetEffectiveForGuildAsync_GuildWideAndBranchOverrideBothExist_BranchOverrideWins()
    {
        const string guildId = "930000000000000007";
        var guildBranchId = await SeedGuildWithBranchAsync(guildId);
        await SeedAsync(db =>
        {
            db.GuildNotificationSettings.Add(new GuildNotificationSetting
            {
                GuildId = guildId, GuildBranchId = null, EventType = GuildNotificationEventType.AbsenceAdded, Enabled = true, ChannelId = "chan-guild-wide",
            });
            db.GuildNotificationSettings.Add(new GuildNotificationSetting
            {
                GuildId = guildId, GuildBranchId = guildBranchId, EventType = GuildNotificationEventType.AbsenceAdded, Enabled = true, ChannelId = "chan-branch-override",
            });
            return Task.CompletedTask;
        });

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildNotificationSettingsRepository>();
            var result = await repo.GetEffectiveForGuildAsync(guildId, guildBranchId);

            result.Should().ContainSingle(s => s.EventType == GuildNotificationEventType.AbsenceAdded
                && s.GuildBranchId == guildBranchId && s.ChannelId == "chan-branch-override");
        }
    }

    // ── DeleteAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_RemovesOnlyBranchRow_GuildWideRowSurvivesAndBecomesEffectiveAgain()
    {
        const string guildId = "930000000000000008";
        var guildBranchId = await SeedGuildWithBranchAsync(guildId);
        await SeedAsync(db =>
        {
            db.GuildNotificationSettings.Add(new GuildNotificationSetting
            {
                GuildId = guildId, GuildBranchId = null, EventType = GuildNotificationEventType.AbsenceAdded, Enabled = true, ChannelId = "chan-guild-wide",
            });
            db.GuildNotificationSettings.Add(new GuildNotificationSetting
            {
                GuildId = guildId, GuildBranchId = guildBranchId, EventType = GuildNotificationEventType.AbsenceAdded, Enabled = true, ChannelId = "chan-branch-override",
            });
            return Task.CompletedTask;
        });

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildNotificationSettingsRepository>();
            await repo.DeleteAsync(guildId, guildBranchId, GuildNotificationEventType.AbsenceAdded);

            var rows = await db.GuildNotificationSettings.Where(s => s.GuildId == guildId).ToListAsync();
            rows.Should().ContainSingle(s => s.GuildBranchId == null && s.ChannelId == "chan-guild-wide");

            var effective = await repo.GetEffectiveForGuildAsync(guildId, guildBranchId);
            effective.Should().ContainSingle(s => s.EventType == GuildNotificationEventType.AbsenceAdded
                && s.GuildBranchId == null && s.ChannelId == "chan-guild-wide");
        }
    }
}
