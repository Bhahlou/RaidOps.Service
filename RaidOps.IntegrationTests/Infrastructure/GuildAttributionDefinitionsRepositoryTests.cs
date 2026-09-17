using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids.Attributions;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.IntegrationTests.Infrastructure;

/// <summary>
/// Integration tests for <see cref="GuildAttributionDefinitionsRepository"/> — in particular the
/// section-aware insertion-position logic in <see cref="GuildAttributionDefinitionsRepository.AddAsync"/>
/// and the bulk <see cref="GuildAttributionDefinitionsRepository.SetSectionIconAsync"/> update, neither
/// of which is exercised end-to-end by the controller tests' happy-path scenarios. All guild IDs are
/// in the 984… range to avoid primary-key conflicts with other test classes.
/// </summary>
[Collection("Integration")]
public class GuildAttributionDefinitionsRepositoryTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private const string CreatedByDiscordId = "984000000000000099";
    private const int HydrossBossId = 14; // Seeded boss of RaidZoneId 4 (Serpentshrine Cavern).

    private static GuildAttributionDefinition MakeDefinition(string guildId, string label, string? section, int? raidBossId = null) => new()
    {
        GuildId = guildId,
        RaidBossId = raidBossId,
        Label = label,
        Section = section,
        CreatedAt = DateTime.UtcNow,
        CreatedByDiscordId = CreatedByDiscordId,
        Cells = [new AttributionDefinitionCell { CellIndex = 0, Kind = AttributionCellKind.NameSlot }],
    };

    private Task SeedGuildAsync(string guildId) => SeedAsync(db =>
    {
        db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
        return Task.CompletedTask;
    });

    [Fact]
    public async Task AddAsync_NoExistingSection_AppendsAtTheEnd()
    {
        const string guildId = "984000000000000001";
        await SeedGuildAsync(guildId);
        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildAttributionDefinitionsRepository>();
            await repo.AddAsync(MakeDefinition(guildId, "Innervate", "Personals"));
            var added = await repo.AddAsync(MakeDefinition(guildId, "Tank swap", "Cooldowns"));

            added.SortOrder.Should().Be(1);
        }
    }

    [Fact]
    public async Task AddAsync_JoiningAnExistingSection_InsertsRightAfterThatSectionsLastRow()
    {
        const string guildId = "984000000000000002";
        await SeedGuildAsync(guildId);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildAttributionDefinitionsRepository>();
            // Personals, Personals, Cooldowns — then a new "Personals" row must land at index 2
            // (right after the second Personals row), pushing "Cooldowns" down to index 3, not
            // appended at index 3 splitting the Personals rows into two groups.
            await repo.AddAsync(MakeDefinition(guildId, "Innervate", "Personals"));
            await repo.AddAsync(MakeDefinition(guildId, "PW: Shield", "Personals"));
            await repo.AddAsync(MakeDefinition(guildId, "Tank swap", "Cooldowns"));

            var added = await repo.AddAsync(MakeDefinition(guildId, "Fear ward", "Personals"));

            added.SortOrder.Should().Be(2);
            var all = await db.GuildAttributionDefinitions.Where(d => d.GuildId == guildId).OrderBy(d => d.SortOrder).ToListAsync();
            all.Select(d => d.Label).Should().Equal("Innervate", "PW: Shield", "Fear ward", "Tank swap");
            all.Select(d => d.SortOrder).Should().Equal(0, 1, 2, 3);
        }
    }

    [Fact]
    public async Task AddAsync_UngroupedRow_AlwaysAppendsAtTheEnd()
    {
        const string guildId = "984000000000000003";
        await SeedGuildAsync(guildId);
        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildAttributionDefinitionsRepository>();
            await repo.AddAsync(MakeDefinition(guildId, "Innervate", "Personals"));
            var added = await repo.AddAsync(MakeDefinition(guildId, "Misc row", section: null));

            added.SortOrder.Should().Be(1);
        }
    }

    [Fact]
    public async Task AddAsync_GeneralAndBossScopesAreNumberedIndependently()
    {
        const string guildId = "984000000000000004";
        await SeedGuildAsync(guildId);
        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildAttributionDefinitionsRepository>();
            await repo.AddAsync(MakeDefinition(guildId, "General row 1", "Personals"));
            await repo.AddAsync(MakeDefinition(guildId, "General row 2", "Personals"));

            // A boss-scoped row joining a same-named "Interrupts" section starts its own count at 0
            // — it must not be pushed down by the two unrelated General rows above.
            var bossRow = await repo.AddAsync(MakeDefinition(guildId, "Interrupt 1", "Interrupts", HydrossBossId));

            bossRow.SortOrder.Should().Be(0);
        }
    }

    [Fact]
    public async Task SetSectionIconAsync_UpdatesEveryRowSharingTheSectionInThatScopeOnly()
    {
        const string guildId = "984000000000000005";
        await SeedGuildAsync(guildId);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildAttributionDefinitionsRepository>();
            await repo.AddAsync(MakeDefinition(guildId, "Innervate", "Personals"));
            await repo.AddAsync(MakeDefinition(guildId, "PW: Shield", "Personals"));
            // Same section name but scoped to a boss — must NOT be touched by the General update below.
            await repo.AddAsync(MakeDefinition(guildId, "Interrupt 1", "Personals", HydrossBossId));

            var updated = await repo.SetSectionIconAsync(guildId, null, "Personals", AttributionIconSource.RaidMarker, null, RaidMarkerIcon.Skull, null);

            updated.Should().Be(2);
            // ExecuteUpdateAsync is a raw bulk SQL update — it never refreshes this context's
            // change tracker, so the already-tracked entities from the AddAsync calls above would
            // otherwise come back stale here.
            var rows = await db.GuildAttributionDefinitions.AsNoTracking().Where(d => d.GuildId == guildId).ToListAsync();
            rows.Where(d => d.RaidBossId == null).Should().OnlyContain(d => d.SectionIconSource == AttributionIconSource.RaidMarker && d.SectionRaidMarker == RaidMarkerIcon.Skull);
            var bossRow = rows.Should().ContainSingle(d => d.RaidBossId == HydrossBossId).Subject;
            bossRow.SectionIconSource.Should().Be(AttributionIconSource.None);
            bossRow.SectionRaidMarker.Should().BeNull();
        }
    }

    [Fact]
    public async Task SetSectionIconAsync_NoRowUsesThatSection_ReturnsZero()
    {
        const string guildId = "984000000000000006";
        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildAttributionDefinitionsRepository>();

            var updated = await repo.SetSectionIconAsync(guildId, null, "No such section", AttributionIconSource.RaidMarker, null, RaidMarkerIcon.Skull, null);

            updated.Should().Be(0);
        }
    }
}
