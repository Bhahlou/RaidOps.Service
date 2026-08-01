using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.IntegrationTests.Infrastructure;

/// <summary>
/// Integration tests for <see cref="RaidZoneRepository"/> — including its handling of
/// <see cref="RaidLockoutCadenceOverride"/> and <see cref="GuildRaidZoneLockout"/> rows, neither of
/// which has an application-layer writer of its own ("rows are inserted directly" per both
/// entities' doc comments) and were otherwise never exercised end-to-end through EF Core.
/// </summary>
[Collection("Integration")]
public class RaidZoneRepositoryTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private const int KarazhanZoneId = 1;
    private const string CreatedByDiscordId = "990000000000000010";

    [Fact]
    public async Task GetAllAsync_ReturnsAllSeededZonesOrderedBySortOrder()
    {
        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRaidZoneRepository>();
            var result = await repo.GetAllAsync();

            result.Should().HaveCountGreaterThanOrEqualTo(8);
            result.Should().BeInAscendingOrder(z => z.SortOrder);
        }
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsZoneWithLockoutCadenceOverrideFieldsRoundTripped()
    {
        var zone = new RaidZone { Id = KarazhanZoneId, Name = "Karazhan" };
        var seededOverride = new RaidLockoutCadenceOverride
        {
            RaidZoneId = KarazhanZoneId,
            RaidZone = zone,
            CadenceDays = 3,
            EffectiveFrom = new DateOnly(2026, 1, 1),
            EffectiveUntil = new DateOnly(2026, 1, 21),
            Reason = "Reset anomaly — temporary 3-day cadence",
            CreatedByDiscordId = CreatedByDiscordId,
            CreatedAt = DateTime.UtcNow,
        };
        // The navigation is set purely for realism when building the row to insert — EF persists
        // via the RaidZoneId FK regardless, and the seeded zone (id 1) already exists (static seed).
        seededOverride.RaidZone.Name.Should().Be("Karazhan");

        await SeedAsync(db =>
        {
            db.RaidLockoutCadenceOverrides.Add(new RaidLockoutCadenceOverride
            {
                RaidZoneId = seededOverride.RaidZoneId,
                CadenceDays = seededOverride.CadenceDays,
                EffectiveFrom = seededOverride.EffectiveFrom,
                EffectiveUntil = seededOverride.EffectiveUntil,
                Reason = seededOverride.Reason,
                CreatedByDiscordId = seededOverride.CreatedByDiscordId,
                CreatedAt = seededOverride.CreatedAt,
            });
            return Task.CompletedTask;
        });

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRaidZoneRepository>();
            var result = await repo.GetByIdAsync(KarazhanZoneId);

            result.Should().NotBeNull();
            var loadedOverride = result!.LockoutOverrides.Should().ContainSingle().Subject;
            loadedOverride.Id.Should().BePositive();
            loadedOverride.RaidZoneId.Should().Be(KarazhanZoneId);
            loadedOverride.Reason.Should().Be("Reset anomaly — temporary 3-day cadence");
            loadedOverride.CreatedByDiscordId.Should().Be(CreatedByDiscordId);
            loadedOverride.CreatedAt.Should().BeCloseTo(seededOverride.CreatedAt, TimeSpan.FromSeconds(1));
        }
    }

    [Fact]
    public async Task GetByIdsAsync_UnknownIds_ReturnsEmpty()
    {
        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRaidZoneRepository>();
            var result = await repo.GetByIdsAsync([999999]);

            result.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task GetGuildOverridesAsync_NoOverridesForGuild_ReturnsEmpty()
    {
        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRaidZoneRepository>();
            var result = await repo.GetGuildOverridesAsync("no-such-guild", [KarazhanZoneId]);

            result.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task GetGuildOverridesAsync_ExistingOverride_ReturnsItWithFieldsRoundTripped()
    {
        const string guildId = "990000000000000011";
        var guild = new Guild { Id = guildId, Name = "Override Test Guild" };
        var zone = new RaidZone { Id = KarazhanZoneId, Name = "Karazhan" };
        var seededCorrection = new GuildRaidZoneLockout
        {
            GuildId = guildId,
            Guild = guild,
            RaidZoneId = KarazhanZoneId,
            RaidZone = zone,
            LockoutAnchorUtc = new DateTime(2026, 1, 1, 4, 0, 0, DateTimeKind.Utc),
            LockoutCadenceDays = 10,
        };
        // Both navigations are set purely for realism when building the row to insert — EF
        // persists via the FK columns regardless, and both referenced rows already exist
        // (the guild is seeded below; the zone is part of the static reference seed).
        seededCorrection.Guild.Name.Should().Be("Override Test Guild");
        seededCorrection.RaidZone.Name.Should().Be("Karazhan");

        await SeedAsync(db =>
        {
            db.Guilds.Add(guild);
            return Task.CompletedTask;
        });
        await SeedAsync(db =>
        {
            db.GuildRaidZoneLockouts.Add(new GuildRaidZoneLockout
            {
                GuildId = seededCorrection.GuildId,
                RaidZoneId = seededCorrection.RaidZoneId,
                LockoutAnchorUtc = seededCorrection.LockoutAnchorUtc,
                LockoutCadenceDays = seededCorrection.LockoutCadenceDays,
            });
            return Task.CompletedTask;
        });

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRaidZoneRepository>();
            var result = await repo.GetGuildOverridesAsync(guildId, [KarazhanZoneId]);

            var found = result.Should().ContainSingle().Subject;
            found.RaidZoneId.Should().Be(KarazhanZoneId);
            found.LockoutCadenceDays.Should().Be(10);
            found.LockoutAnchorUtc.Should().Be(seededCorrection.LockoutAnchorUtc);
        }
    }
}
