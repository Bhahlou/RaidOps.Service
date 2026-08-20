using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.IntegrationTests.Infrastructure;

/// <summary>
/// Integration tests for <see cref="RaidEventRepository"/> members not already exercised end-to-end
/// through <c>RaidsController</c> — its own defensive not-found guards in
/// <see cref="IRaidEventRepository.UpdateAsync"/>/<see cref="IRaidEventRepository.DeleteAsync"/>
/// (unreachable through the controller, which already re-fetches and fails fast before calling
/// into these), plus the standing-embed reference helpers and the dedicated-channel bookkeeping
/// used by the raid channel picker/move/delete feature.
/// </summary>
[Collection("Integration")]
public class RaidEventRepositoryTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private const int KarazhanZoneId = 1;

    [Fact]
    public async Task UpdateAsync_EventNotFound_ReturnsFalse()
    {
        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRaidEventRepository>();
            var result = await repo.UpdateAsync(
                new RaidEvent { Id = -1, Name = "Ghost event", GroupCount = 2, SlotsPerGroup = 5 },
                guildBranchId: -1,
                raidZoneIds: [1]);

            result.Should().BeFalse();
        }
    }

    [Fact]
    public async Task DeleteAsync_EventNotFound_ReturnsFalse()
    {
        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRaidEventRepository>();
            var result = await repo.DeleteAsync(id: -1, guildBranchId: -1);

            result.Should().BeFalse();
        }
    }

    // ── UpdateCompositionAnnouncementReferenceAsync ──────────────────────────

    [Fact]
    public async Task UpdateCompositionAnnouncementReferenceAsync_Success_PersistsChannelAndMessageId()
    {
        const string guildId = "990000000000000020";
        const string discordId = "990000000000000020";
        var branch = TestDataBuilder.CreateGuildBranch(guildId, branchId: 4);
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(discordId));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.GuildBranches.Add(branch);
            return Task.CompletedTask;
        });

        int eventId;
        var (seedScope, seedDb) = CreateDbScope();
        using (seedScope)
        {
            var raidEvent = new RaidEvent
            {
                GuildId = guildId,
                GuildBranchId = branch.Id,
                Name = "Announcement Ref Event",
                StartsAtUtc = DateTime.UtcNow.AddDays(1),
                GroupCount = 2,
                SlotsPerGroup = 5,
                CreatedByDiscordId = discordId,
                CreatedAt = DateTime.UtcNow,
            };
            seedDb.RaidEvents.Add(raidEvent);
            await seedDb.SaveChangesAsync();
            eventId = raidEvent.Id;
        }

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRaidEventRepository>();
            await repo.UpdateCompositionAnnouncementReferenceAsync(eventId, branch.Id, channelId: "555", messageId: "777");

            var updated = await db.RaidEvents.FindAsync(eventId);
            updated!.CompositionAnnouncementChannelId.Should().Be("555");
            updated.CompositionAnnouncementMessageId.Should().Be("777");
        }
    }

    // ── UpdateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_Success_PersistsDedicatedChannelFields()
    {
        const string guildId = "990000000000000030";
        const string discordId = "990000000000000030";
        var branch = TestDataBuilder.CreateGuildBranch(guildId, branchId: 4);
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(discordId));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.GuildBranches.Add(branch);
            return Task.CompletedTask;
        });

        int eventId;
        var (seedScope, seedDb) = CreateDbScope();
        using (seedScope)
        {
            var raidEvent = new RaidEvent
            {
                GuildId = guildId,
                GuildBranchId = branch.Id,
                Name = "Channel Update Event",
                StartsAtUtc = DateTime.UtcNow.AddDays(1),
                GroupCount = 2,
                SlotsPerGroup = 5,
                CreatedByDiscordId = discordId,
                CreatedAt = DateTime.UtcNow,
            };
            seedDb.RaidEvents.Add(raidEvent);
            await seedDb.SaveChangesAsync();
            eventId = raidEvent.Id;
        }

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRaidEventRepository>();
            var updated = await repo.UpdateAsync(
                new RaidEvent
                {
                    Id = eventId,
                    Name = "Channel Update Event",
                    GroupCount = 2,
                    SlotsPerGroup = 5,
                    DedicatedAnnouncementChannelId = "555",
                    DedicatedAnnouncementChannelIsBotOwned = true,
                },
                branch.Id,
                raidZoneIds: [KarazhanZoneId]);

            updated.Should().BeTrue();

            var persisted = await db.RaidEvents.FindAsync(eventId);
            persisted!.DedicatedAnnouncementChannelId.Should().Be("555");
            persisted.DedicatedAnnouncementChannelIsBotOwned.Should().BeTrue();
        }
    }

    // ── UpdateSignupCallAnnouncementReferenceAsync ───────────────────────────

    [Fact]
    public async Task UpdateSignupCallAnnouncementReferenceAsync_Success_PersistsChannelAndMessageId()
    {
        const string guildId = "990000000000000031";
        const string discordId = "990000000000000031";
        var branch = TestDataBuilder.CreateGuildBranch(guildId, branchId: 4);
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(discordId));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.GuildBranches.Add(branch);
            return Task.CompletedTask;
        });

        int eventId;
        var (seedScope, seedDb) = CreateDbScope();
        using (seedScope)
        {
            var raidEvent = new RaidEvent
            {
                GuildId = guildId,
                GuildBranchId = branch.Id,
                Name = "Signup Ref Event",
                StartsAtUtc = DateTime.UtcNow.AddDays(1),
                GroupCount = 2,
                SlotsPerGroup = 5,
                CreatedByDiscordId = discordId,
                CreatedAt = DateTime.UtcNow,
            };
            seedDb.RaidEvents.Add(raidEvent);
            await seedDb.SaveChangesAsync();
            eventId = raidEvent.Id;
        }

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRaidEventRepository>();
            await repo.UpdateSignupCallAnnouncementReferenceAsync(eventId, branch.Id, channelId: "555", messageId: "777");

            var updated = await db.RaidEvents.FindAsync(eventId);
            updated!.SignupCallAnnouncementChannelId.Should().Be("555");
            updated.SignupCallAnnouncementMessageId.Should().Be("777");
        }
    }

    // ── ClearAnnouncementReferencesAsync ─────────────────────────────────────

    [Fact]
    public async Task ClearAnnouncementReferencesAsync_Success_ClearsAllFourFields()
    {
        const string guildId = "990000000000000032";
        const string discordId = "990000000000000032";
        var branch = TestDataBuilder.CreateGuildBranch(guildId, branchId: 4);
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(discordId));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.GuildBranches.Add(branch);
            return Task.CompletedTask;
        });

        int eventId;
        var (seedScope, seedDb) = CreateDbScope();
        using (seedScope)
        {
            var raidEvent = new RaidEvent
            {
                GuildId = guildId,
                GuildBranchId = branch.Id,
                Name = "Clear Refs Event",
                StartsAtUtc = DateTime.UtcNow.AddDays(1),
                GroupCount = 2,
                SlotsPerGroup = 5,
                CreatedByDiscordId = discordId,
                CreatedAt = DateTime.UtcNow,
                CompositionAnnouncementChannelId = "111",
                CompositionAnnouncementMessageId = "222",
                SignupCallAnnouncementChannelId = "333",
                SignupCallAnnouncementMessageId = "444",
            };
            seedDb.RaidEvents.Add(raidEvent);
            await seedDb.SaveChangesAsync();
            eventId = raidEvent.Id;
        }

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRaidEventRepository>();
            await repo.ClearAnnouncementReferencesAsync(eventId, branch.Id);

            var updated = await db.RaidEvents.FindAsync(eventId);
            updated!.CompositionAnnouncementChannelId.Should().BeNull();
            updated.CompositionAnnouncementMessageId.Should().BeNull();
            updated.SignupCallAnnouncementChannelId.Should().BeNull();
            updated.SignupCallAnnouncementMessageId.Should().BeNull();
        }
    }

    // ── DeleteEmptyDraftOccurrencesForSeriesAsync ────────────────────────────

    [Fact]
    public async Task DeleteEmptyDraftOccurrencesForSeriesAsync_ReturnsBotOwnedChannelIdsOfDeletedOccurrencesOnly()
    {
        const string guildId = "990000000000000033";
        const string discordId = "990000000000000033";
        var branch = TestDataBuilder.CreateGuildBranch(guildId, branchId: 4);
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(discordId));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.GuildBranches.Add(branch);
            return Task.CompletedTask;
        });

        var series = new RaidSeries
        {
            GuildId = guildId,
            GuildBranchId = branch.Id,
            Name = "Split 1",
            RecurrenceDayOfWeek = DayOfWeek.Wednesday,
            RecurrenceStartTimeLocal = new TimeOnly(20, 0),
            RecurrenceIntervalWeeks = 1,
            GroupCount = 2,
            SlotsPerGroup = 5,
            CreatedByDiscordId = discordId,
            CreatedAt = DateTime.UtcNow,
        };
        await SeedAsync(db =>
        {
            db.RaidSeries.Add(series);
            return Task.CompletedTask;
        });

        int botOwnedEventId, notBotOwnedEventId, noChannelEventId;
        var (seedScope, seedDb) = CreateDbScope();
        using (seedScope)
        {
            var botOwned = new RaidEvent
            {
                GuildId = guildId, GuildBranchId = branch.Id, RaidSeriesId = series.Id, Name = "Bot-owned",
                StartsAtUtc = DateTime.UtcNow, GroupCount = 2, SlotsPerGroup = 5,
                CreatedByDiscordId = discordId, CreatedAt = DateTime.UtcNow,
                DedicatedAnnouncementChannelId = "999", DedicatedAnnouncementChannelIsBotOwned = true,
            };
            var notBotOwned = new RaidEvent
            {
                GuildId = guildId, GuildBranchId = branch.Id, RaidSeriesId = series.Id, Name = "Existing channel",
                StartsAtUtc = DateTime.UtcNow, GroupCount = 2, SlotsPerGroup = 5,
                CreatedByDiscordId = discordId, CreatedAt = DateTime.UtcNow,
                DedicatedAnnouncementChannelId = "888", DedicatedAnnouncementChannelIsBotOwned = false,
            };
            var noChannel = new RaidEvent
            {
                GuildId = guildId, GuildBranchId = branch.Id, RaidSeriesId = series.Id, Name = "No channel",
                StartsAtUtc = DateTime.UtcNow, GroupCount = 2, SlotsPerGroup = 5,
                CreatedByDiscordId = discordId, CreatedAt = DateTime.UtcNow,
            };
            seedDb.RaidEvents.AddRange(botOwned, notBotOwned, noChannel);
            await seedDb.SaveChangesAsync();
            botOwnedEventId = botOwned.Id;
            notBotOwnedEventId = notBotOwned.Id;
            noChannelEventId = noChannel.Id;
        }

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRaidEventRepository>();
            var (deletedCount, botOwnedChannelIds) = await repo.DeleteEmptyDraftOccurrencesForSeriesAsync(series.Id, branch.Id);

            deletedCount.Should().Be(3);
            botOwnedChannelIds.Should().ContainSingle().Which.Should().Be("999");

            (await db.RaidEvents.AnyAsync(e => e.Id == botOwnedEventId)).Should().BeFalse();
            (await db.RaidEvents.AnyAsync(e => e.Id == notBotOwnedEventId)).Should().BeFalse();
            (await db.RaidEvents.AnyAsync(e => e.Id == noChannelEventId)).Should().BeFalse();
        }
    }

    // ── GetUpcomingPublishedForGuildAsync ─────────────────────────────────────

    [Fact]
    public async Task GetUpcomingPublishedForGuildAsync_ExcludesDraftAndPastEvents_IncludesUpcomingPublishedOnes()
    {
        const string guildId = "990000000000000021";
        const string discordId = "990000000000000021";
        var fromUtc = DateTime.UtcNow;
        var branch = TestDataBuilder.CreateGuildBranch(guildId, branchId: 4);

        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(discordId));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.GuildBranches.Add(branch);
            return Task.CompletedTask;
        });

        var (seedScope, seedDb) = CreateDbScope();
        using (seedScope)
        {
            seedDb.RaidEvents.AddRange(
                new RaidEvent
                {
                    GuildId = guildId,
                    GuildBranchId = branch.Id,
                    Name = "Upcoming Published",
                    StartsAtUtc = fromUtc.AddDays(1),
                    PublicationStatus = RaidPublicationStatus.Published,
                    GroupCount = 2,
                    SlotsPerGroup = 5,
                    CreatedByDiscordId = discordId,
                    CreatedAt = DateTime.UtcNow,
                },
                new RaidEvent
                {
                    GuildId = guildId,
                    GuildBranchId = branch.Id,
                    Name = "Upcoming Draft",
                    StartsAtUtc = fromUtc.AddDays(1),
                    PublicationStatus = RaidPublicationStatus.Draft,
                    GroupCount = 2,
                    SlotsPerGroup = 5,
                    CreatedByDiscordId = discordId,
                    CreatedAt = DateTime.UtcNow,
                },
                new RaidEvent
                {
                    GuildId = guildId,
                    GuildBranchId = branch.Id,
                    Name = "Past Published",
                    StartsAtUtc = fromUtc.AddDays(-1),
                    PublicationStatus = RaidPublicationStatus.Published,
                    GroupCount = 2,
                    SlotsPerGroup = 5,
                    CreatedByDiscordId = discordId,
                    CreatedAt = DateTime.UtcNow,
                });
            await seedDb.SaveChangesAsync();
        }

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRaidEventRepository>();
            var events = await repo.GetUpcomingPublishedForGuildAsync(guildId, fromUtc, limit: 25);

            events.Should().ContainSingle();
            events[0].Name.Should().Be("Upcoming Published");
            events[0].GuildBranch.Should().NotBeNull();
            events[0].GuildBranch.Branch.Should().NotBeNull();
        }
    }
}
