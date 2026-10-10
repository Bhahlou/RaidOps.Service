using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids.Attributions;
using RaidOps.Infrastructure.Persistence.Implementations;
using System.Data.Common;
using Testcontainers.PostgreSql;

namespace RaidOps.IntegrationTests.Infrastructure;

/// <summary>
/// Proves that migration <c>SpellContentPerExpansionAndBranchScopedAttributions</c> is lossless for the
/// spell reference data: on its own throw-away Postgres container, the schema is migrated to the
/// previous migration, filled with data in the old shape (name/icon on <c>Spells</c>, name-less
/// <c>SpellAvailabilities</c> rows, a guild-wide attribution template), migrated to the target, and the
/// result is inspected. Deliberately not part of the shared "Integration" collection — it must own its
/// database, since it drives the schema through history rather than using the fully migrated test host.
/// </summary>
public class SpellContentMigrationTests : IAsyncLifetime
{
    private const string PreviousMigration = "20260923192610_AddSpellAvailabilityAndBranchWagoSync";
    private const string TargetMigration = "20260924084553_SpellContentPerExpansionAndBranchScopedAttributions";

    private const string GuildId = "989000000000000001";
    private const int SpellWithTwoExpansions = 9890001;
    private const int SpellWithOneExpansionAndNoIcon = 9890002;
    private const int SpellWithoutAnyAvailability = 9890003;

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("raidops_migration")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private RaidOpsDbContext CreateContext()
        => new(new DbContextOptionsBuilder<RaidOpsDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);

    private static async Task ExecuteAsync(RaidOpsDbContext db, string sql)
        => await db.Database.ExecuteSqlRawAsync(sql);

    private static async Task<List<object?[]>> QueryAsync(RaidOpsDbContext db, string sql)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        await using DbCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();

        var rows = new List<object?[]>();
        while (await reader.ReadAsync())
        {
            var values = new object?[reader.FieldCount];
            for (var i = 0; i < values.Length; i++)
                values[i] = await reader.IsDBNullAsync(i) ? null : reader.GetValue(i);
            rows.Add(values);
        }

        return rows;
    }

    private static async Task<long> CountAsync(RaidOpsDbContext db, string table)
        => (long)(await QueryAsync(db, $"""SELECT COUNT(*) FROM "{table}";"""))[0][0]!;

    [Fact]
    public async Task Up_CopiesNameAndIconToEveryAvailabilityRowDropsTheSpellColumnsAndClearsGuildWideTemplates()
    {
        // ── Arrange: the schema and data as they were before this migration ──
        await using (var db = CreateContext())
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(PreviousMigration);

            // Old shape: content lives on Spells, SpellAvailabilities only holds (SpellId, ExpansionId).
            await ExecuteAsync(db, $"""
                INSERT INTO "Spells" ("Id", "NameEn", "NameFr", "NameDe", "IconUrl") VALUES
                  ({SpellWithTwoExpansions}, 'Alpha', 'Alpha FR', 'Alpha DE', 'https://cdn/alpha.jpg'),
                  ({SpellWithOneExpansionAndNoIcon}, 'Beta', 'Beta FR', 'Beta DE', ''),
                  ({SpellWithoutAnyAvailability}, 'Gamma', 'Gamma FR', 'Gamma DE', 'https://cdn/gamma.jpg');
                INSERT INTO "SpellAvailabilities" ("SpellId", "ExpansionId") VALUES
                  ({SpellWithTwoExpansions}, 2), ({SpellWithTwoExpansions}, 12), ({SpellWithOneExpansionAndNoIcon}, 2);
                """);

            // A guild-wide (branch-less) template with a cell and a section icon pointing at a spell.
            db.Guilds.Add(TestDataBuilder.CreateGuild(GuildId, isRegistered: true));
            await db.SaveChangesAsync();
            await ExecuteAsync(db, $"""
                INSERT INTO "GuildAttributionDefinitions"
                  ("GuildId", "Label", "Section", "IsRepeatable", "SortOrder", "SectionIconSource", "SectionSpellId", "CreatedAt", "CreatedByDiscordId")
                VALUES ('{GuildId}', 'Bloodlust', 'Cooldowns', false, 0, 1, {SpellWithTwoExpansions}, now(), 'officer-1');
                INSERT INTO "AttributionDefinitionCells"
                  ("CellIndex", "GuildAttributionDefinitionId", "IconSource", "Kind", "SpellId", "RequiredClassIds", "RequiredRoles", "RequiredSpecIds")
                SELECT 0, "Id", 1, 0, {SpellWithTwoExpansions}, ARRAY[]::integer[], ARRAY[]::integer[], ARRAY[]::integer[] FROM "GuildAttributionDefinitions" WHERE "GuildId" = '{GuildId}';
                """);
            (await CountAsync(db, "GuildAttributionDefinitions")).Should().Be(1);
            (await CountAsync(db, "AttributionDefinitionCells")).Should().Be(1);
        }

        // ── Act ──
        await using (var db = CreateContext())
            await db.GetService<IMigrator>().MigrateAsync(TargetMigration);

        // ── Assert ──
        await using (var db = CreateContext())
        {
            // Every availability row got its spell's name/icon — including both expansions of the shared spell.
            var availabilities = await QueryAsync(db, """
                SELECT "SpellId", "ExpansionId", "NameEn", "NameFr", "NameDe", "IconUrl"
                FROM "SpellAvailabilities" WHERE "SpellId" BETWEEN 9890001 AND 9890003 ORDER BY "SpellId", "ExpansionId";
                """);
            availabilities.Select(r => (SpellId: (int)r[0]!, ExpansionId: (int)r[1]!, NameEn: (string)r[2]!, NameFr: (string)r[3]!, NameDe: (string)r[4]!, IconUrl: (string)r[5]!))
                .Should().Equal(
                    (SpellWithTwoExpansions, 2, "Alpha", "Alpha FR", "Alpha DE", "https://cdn/alpha.jpg"),
                    (SpellWithTwoExpansions, 12, "Alpha", "Alpha FR", "Alpha DE", "https://cdn/alpha.jpg"),
                    (SpellWithOneExpansionAndNoIcon, 2, "Beta", "Beta FR", "Beta DE", string.Empty));

            // The spells themselves survive (including the one with no availability row), minus the moved columns.
            (await CountAsync(db, "Spells")).Should().Be(3);
            var spellColumns = await QueryAsync(db, """
                SELECT column_name FROM information_schema.columns WHERE table_name = 'Spells' ORDER BY column_name;
                """);
            spellColumns.Select(r => (string)r[0]!).Should().Equal("Id");

            // The guild-wide template is gone, cascading to its cells; the new branch column exists.
            (await CountAsync(db, "GuildAttributionDefinitions")).Should().Be(0);
            (await CountAsync(db, "AttributionDefinitionCells")).Should().Be(0);
            var definitionColumns = await QueryAsync(db, """
                SELECT column_name FROM information_schema.columns WHERE table_name = 'GuildAttributionDefinitions' AND column_name = 'GuildBranchId';
                """);
            definitionColumns.Should().ContainSingle();

            // The migrated schema matches the EF model: read the content back through the entities, and
            // create a branch-scoped template row against it.
            var alphaOnForever = await db.SpellAvailabilities.AsNoTracking().SingleAsync(a => a.SpellId == SpellWithTwoExpansions && a.ExpansionId == 12);
            alphaOnForever.NameFr.Should().Be("Alpha FR");

            var guildBranch = TestDataBuilder.CreateGuildBranch(GuildId, branchId: 5);
            db.GuildBranches.Add(guildBranch);
            await db.SaveChangesAsync();
            db.GuildAttributionDefinitions.Add(new GuildAttributionDefinition
            {
                GuildId = GuildId,
                GuildBranchId = guildBranch.Id,
                Label = "After migration",
                CreatedAt = DateTime.UtcNow,
                CreatedByDiscordId = "officer-1",
                Cells = [new AttributionDefinitionCell { CellIndex = 0, Kind = AttributionCellKind.NameSlot }],
            });
            await db.SaveChangesAsync();
            (await CountAsync(db, "GuildAttributionDefinitions")).Should().Be(1);
        }
    }
}
