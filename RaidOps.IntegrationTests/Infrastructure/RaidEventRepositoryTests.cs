using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.IntegrationTests.Infrastructure;

/// <summary>
/// Integration tests for <see cref="RaidEventRepository"/>'s own defensive not-found guards in
/// <see cref="IRaidEventRepository.UpdateAsync"/> and <see cref="IRaidEventRepository.DeleteAsync"/>.
/// Both are unreachable through <c>RaidsController</c>'s handlers, which already re-fetch the event
/// via <c>GetByIdAsync</c> and fail fast before ever calling into these — so only a direct
/// repository call exercises the repository's own guard rather than the handler's.
/// </summary>
[Collection("Integration")]
public class RaidEventRepositoryTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
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
