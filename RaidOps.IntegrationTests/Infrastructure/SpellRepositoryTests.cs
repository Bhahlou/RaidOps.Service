using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Implementations;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.IntegrationTests.Infrastructure;

/// <summary>
/// Integration tests for <see cref="SpellRepository"/> — spell IDs are in the 9990… / 986… ranges, well
/// above any real Blizzard spell ID, so they never collide with spells other tests seed.
/// </summary>
[Collection("Integration")]
public class SpellRepositoryTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private const int TbcExpansionId = 2;

    private static SpellAvailability MakeAvailability(int spellId, string nameEn, string nameFr, string nameDe, int expansionId = TbcExpansionId) => new()
    {
        SpellId = spellId,
        ExpansionId = expansionId,
        NameEn = nameEn,
        NameFr = nameFr,
        NameDe = nameDe,
        IconUrl = "https://cdn/icon.jpg",
    };

    private static void AddSpell(RaidOpsDbContext db, int spellId, string nameEn, string nameFr, string nameDe)
    {
        db.Spells.Add(new Spell { Id = spellId });
        db.SpellAvailabilities.Add(MakeAvailability(spellId, nameEn, nameFr, nameDe));
    }

    [Fact]
    public async Task GetAvailabilityAsync_ExistingId_ReturnsSpell()
    {
        await SeedAsync(db =>
        {
            AddSpell(db, 9990001, "Earth Shock", "Horion de terre", "Erdschock");
            return Task.CompletedTask;
        });

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<ISpellRepository>();
            var result = await repo.GetAvailabilityAsync(9990001, TbcExpansionId);

            result.Should().NotBeNull();
            result!.NameEn.Should().Be("Earth Shock");
        }
    }

    [Fact]
    public async Task GetAvailabilityAsync_UnknownId_ReturnsNull()
    {
        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<ISpellRepository>();
            var result = await repo.GetAvailabilityAsync(999999999, TbcExpansionId);

            result.Should().BeNull();
        }
    }

    [Fact]
    public async Task SearchAsync_LocaleDe_SearchesAndOrdersByGermanName()
    {
        // A search term synthetic enough that no real seeded TBC spell can possibly contain it —
        // otherwise the exact-match assertion below could pick up unrelated real spells too.
        await SeedAsync(db =>
        {
            AddSpell(db, 9990010, "Innervate", "Innervation", "ZqxvtestZweit");
            AddSpell(db, 9990011, "Renew", "Renouveau", "ZqxvtestAnfang");
            return Task.CompletedTask;
        });

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<ISpellRepository>();
            var result = await repo.SearchAsync(TbcExpansionId, "Zqxvtest", "de", 10);

            // Both German names contain "Zqxvtest" — ordering by NameDe puts "Anfang" before "Zweit".
            result.Select(s => s.SpellId).Should().Equal(9990011, 9990010);
        }
    }

    [Fact]
    public async Task SearchAsync_UnknownLocale_FallsBackToEnglishNameAndOrder()
    {
        await SeedAsync(db =>
        {
            AddSpell(db, 9990020, "ZqxvtestZzz Ability", "Zzz Capacité", "Zzz Fähigkeit");
            AddSpell(db, 9990021, "ZqxvtestAaa Ability", "Aaa Capacité", "Aaa Fähigkeit");
            return Task.CompletedTask;
        });

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<ISpellRepository>();
            var result = await repo.SearchAsync(TbcExpansionId, "Zqxvtest", "es", 10);

            result.Select(s => s.SpellId).Should().Equal(9990021, 9990020);
        }
    }

    [Fact]
    public async Task UpsertAsync_AllAlreadyExistUnchanged_ReturnsEmptyDiffAndDoesNotDuplicate()
    {
        await SeedAsync(db =>
        {
            AddSpell(db, 9990030, "Existing", "Existant", "Vorhanden");
            return Task.CompletedTask;
        });

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<ISpellRepository>();
            var diff = await repo.UpsertAsync([MakeAvailability(9990030, "Existing", "Existant", "Vorhanden")]);

            diff.Added.Should().BeEmpty();
            diff.Renamed.Should().BeEmpty();
            var count = db.Spells.Count(s => s.Id == 9990030);
            count.Should().Be(1);
        }
    }

    // ── Per-expansion content (IDs in the 986… range) ────────────────────────

    private const int ForeverExpansionId = 12;

    private async Task<SpellAvailability?> ReadAvailabilityAsync(int spellId, int expansionId)
    {
        var (scope, db) = CreateDbScope();
        using (scope)
            return await db.SpellAvailabilities.AsNoTracking().SingleOrDefaultAsync(a => a.SpellId == spellId && a.ExpansionId == expansionId);
    }

    private async Task<SpellSyncDiff> UpsertAsync(params SpellAvailability[] rows)
    {
        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<ISpellRepository>();
            return await repo.UpsertAsync(rows);
        }
    }

    [Fact]
    public async Task GetAvailabilityAsync_SpellOnlyOnAnotherExpansion_ReturnsNull()
    {
        await SeedAsync(db =>
        {
            AddSpell(db, 9860001, "Only On TBC", "Seulement TBC", "Nur TBC");
            return Task.CompletedTask;
        });

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<ISpellRepository>();

            (await repo.GetAvailabilityAsync(9860001, ForeverExpansionId)).Should().BeNull();
            (await repo.GetAvailabilityAsync(9860001, TbcExpansionId)).Should().NotBeNull();
        }
    }

    [Fact]
    public async Task GetAvailabilityAsync_SameSpellOnTwoExpansions_ReturnsEachExpansionsOwnContent()
    {
        await SeedAsync(db =>
        {
            db.Spells.Add(new Spell { Id = 9860002 });
            db.SpellAvailabilities.Add(MakeAvailability(9860002, "Name On TBC", "Nom TBC", "Name TBC", TbcExpansionId));
            db.SpellAvailabilities.Add(MakeAvailability(9860002, "Name On Forever", "Nom Forever", "Name Forever", ForeverExpansionId));
            return Task.CompletedTask;
        });

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<ISpellRepository>();

            (await repo.GetAvailabilityAsync(9860002, TbcExpansionId))!.NameEn.Should().Be("Name On TBC");
            (await repo.GetAvailabilityAsync(9860002, ForeverExpansionId))!.NameEn.Should().Be("Name On Forever");
        }
    }

    [Fact]
    public async Task SearchAsync_LocaleEn_MatchesCaseInsensitivelyAndOrdersByEnglishName()
    {
        await SeedAsync(db =>
        {
            AddSpell(db, 9860010, "Zqxensearch Bravo", "Zzz", "Zzz");
            AddSpell(db, 9860011, "ZQXENSEARCH ALPHA", "Aaa", "Aaa");
            AddSpell(db, 9860012, "Unrelated", "Zqxensearch Fr", "Zqxensearch De");
            return Task.CompletedTask;
        });

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<ISpellRepository>();
            var result = await repo.SearchAsync(TbcExpansionId, "zqxensearch", "en", 10);

            result.Select(s => s.SpellId).Should().Equal(9860011, 9860010);
        }
    }

    [Fact]
    public async Task SearchAsync_LocaleFr_SearchesAndOrdersByFrenchName()
    {
        await SeedAsync(db =>
        {
            AddSpell(db, 9860020, "Aaa", "Zqxfrsearch Zeta", "Aaa");
            AddSpell(db, 9860021, "Zqxfrsearch English Only", "Zqxfrsearch Alpha", "Zzz");
            AddSpell(db, 9860022, "Zqxfrsearch English Only 2", "Unrelated", "Unrelated");
            return Task.CompletedTask;
        });

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<ISpellRepository>();
            var result = await repo.SearchAsync(TbcExpansionId, "Zqxfrsearch", "fr", 10);

            result.Select(s => s.SpellId).Should().Equal(9860021, 9860020);
        }
    }

    [Fact]
    public async Task SearchAsync_LimitSmallerThanMatches_ReturnsOnlyTheFirstNInOrder()
    {
        await SeedAsync(db =>
        {
            AddSpell(db, 9860030, "Zqxlimit C", "C", "C");
            AddSpell(db, 9860031, "Zqxlimit A", "A", "A");
            AddSpell(db, 9860032, "Zqxlimit B", "B", "B");
            return Task.CompletedTask;
        });

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<ISpellRepository>();
            var result = await repo.SearchAsync(TbcExpansionId, "Zqxlimit", "en", 2);

            result.Select(s => s.SpellId).Should().Equal(9860031, 9860032);
        }
    }

    [Fact]
    public async Task SearchAsync_OnlyReturnsSpellsObservedOnTheRequestedExpansion_WithThatExpansionsName()
    {
        await SeedAsync(db =>
        {
            db.Spells.AddRange(new Spell { Id = 9860040 }, new Spell { Id = 9860041 });
            db.SpellAvailabilities.Add(MakeAvailability(9860040, "Zqxexp Tbc Name", "x", "x", TbcExpansionId));
            db.SpellAvailabilities.Add(MakeAvailability(9860040, "Zqxexp Forever Name", "x", "x", ForeverExpansionId));
            db.SpellAvailabilities.Add(MakeAvailability(9860041, "Zqxexp Forever Only", "x", "x", ForeverExpansionId));
            return Task.CompletedTask;
        });

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<ISpellRepository>();

            var tbc = await repo.SearchAsync(TbcExpansionId, "Zqxexp", "en", 10);
            var forever = await repo.SearchAsync(ForeverExpansionId, "Zqxexp", "en", 10);

            tbc.Select(s => (s.SpellId, s.NameEn)).Should().Equal((9860040, "Zqxexp Tbc Name"));
            forever.Select(s => (s.SpellId, s.NameEn)).Should().Equal((9860040, "Zqxexp Forever Name"), (9860041, "Zqxexp Forever Only"));
        }
    }

    // ── UpsertAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpsertAsync_NewSpell_CreatesTheBareSpellAndItsAvailabilityRow()
    {
        var diff = await UpsertAsync( MakeAvailability(9860050, "Brand New", "Tout neuf", "Brandneu"));

        diff.Added.Should().ContainSingle();
        diff.Added[0].SpellId.Should().Be(9860050);
        diff.Added[0].NameEn.Should().Be("Brand New");
        diff.Added[0].PreviousNameEn.Should().BeNull();
        diff.Renamed.Should().BeEmpty();
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            (await db.Spells.CountAsync(s => s.Id == 9860050)).Should().Be(1);
            var row = await db.SpellAvailabilities.AsNoTracking().SingleAsync(a => a.SpellId == 9860050);
            row.ExpansionId.Should().Be(TbcExpansionId);
            row.NameFr.Should().Be("Tout neuf");
            row.NameDe.Should().Be("Brandneu");
            row.IconUrl.Should().Be("https://cdn/icon.jpg");
        }
    }

    [Fact]
    public async Task UpsertAsync_TwoNewRowsForTheSameNewSpell_CreateOnlyOneSpell()
    {
        var diff = await UpsertAsync(
            MakeAvailability(9860051, "Shared", "Partage", "Geteilt", TbcExpansionId),
            MakeAvailability(9860051, "Shared", "Partage", "Geteilt", ForeverExpansionId));

        diff.Added.Should().HaveCount(2);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            (await db.Spells.CountAsync(s => s.Id == 9860051)).Should().Be(1);
            (await db.SpellAvailabilities.CountAsync(a => a.SpellId == 9860051)).Should().Be(2);
        }
    }

    [Fact]
    public async Task UpsertAsync_ExistingSpellOnANewExpansion_OnlyAddsTheAvailabilityRow()
    {
        await SeedAsync(db =>
        {
            AddSpell(db, 9860052, "On TBC", "Sur TBC", "Auf TBC");
            return Task.CompletedTask;
        });

        var diff = await UpsertAsync( MakeAvailability(9860052, "On Forever", "Sur Forever", "Auf Forever", ForeverExpansionId));

        diff.Added.Select(e => e.SpellId).Should().Equal(9860052);
        diff.Renamed.Should().BeEmpty();
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            (await db.Spells.CountAsync(s => s.Id == 9860052)).Should().Be(1);
            (await db.SpellAvailabilities.CountAsync(a => a.SpellId == 9860052)).Should().Be(2);
        }
        (await ReadAvailabilityAsync(9860052, TbcExpansionId))!.NameEn.Should().Be("On TBC");
        (await ReadAvailabilityAsync(9860052, ForeverExpansionId))!.NameEn.Should().Be("On Forever");
    }

    [Fact]
    public async Task UpsertAsync_RenamedEnglishName_IsReportedWithThePreviousNameAndPersisted()
    {
        await SeedAsync(db =>
        {
            AddSpell(db, 9860053, "Old Name", "Ancien", "Alt");
            return Task.CompletedTask;
        });

        var diff = await UpsertAsync( MakeAvailability(9860053, "New Name", "Nouveau", "Neu"));

        diff.Added.Should().BeEmpty();
        var renamed = diff.Renamed.Should().ContainSingle().Subject;
        renamed.SpellId.Should().Be(9860053);
        renamed.NameEn.Should().Be("New Name");
        renamed.PreviousNameEn.Should().Be("Old Name");
        var row = await ReadAvailabilityAsync(9860053, TbcExpansionId);
        row!.NameEn.Should().Be("New Name");
        row.NameFr.Should().Be("Nouveau");
        row.NameDe.Should().Be("Neu");
    }

    [Fact]
    public async Task UpsertAsync_OnlyFrenchAndGermanChanged_UpdatesThemWithoutReportingARename()
    {
        await SeedAsync(db =>
        {
            AddSpell(db, 9860054, "Same English", "Ancien", "Alt");
            return Task.CompletedTask;
        });

        var diff = await UpsertAsync( MakeAvailability(9860054, "Same English", "Nouveau", "Neu"));

        diff.Added.Should().BeEmpty();
        diff.Renamed.Should().BeEmpty();
        var row = await ReadAvailabilityAsync(9860054, TbcExpansionId);
        row!.NameFr.Should().Be("Nouveau");
        row.NameDe.Should().Be("Neu");
    }

    [Fact]
    public async Task UpsertAsync_ChangedIcon_UpdatesTheIcon()
    {
        await SeedAsync(db =>
        {
            AddSpell(db, 9860055, "Iconic", "Iconique", "Ikonisch");
            return Task.CompletedTask;
        });
        var incoming = MakeAvailability(9860055, "Iconic", "Iconique", "Ikonisch");
        incoming.IconUrl = "https://cdn/new-icon.jpg";

        var diff = await UpsertAsync( incoming);

        diff.Added.Should().BeEmpty();
        diff.Renamed.Should().BeEmpty();
        (await ReadAvailabilityAsync(9860055, TbcExpansionId))!.IconUrl.Should().Be("https://cdn/new-icon.jpg");
    }

    [Fact]
    public async Task UpsertAsync_EmptyIncomingIcon_DoesNotBlankAnExistingIcon()
    {
        await SeedAsync(db =>
        {
            AddSpell(db, 9860056, "Keeps Icon", "Garde", "Behaelt");
            return Task.CompletedTask;
        });
        var incoming = MakeAvailability(9860056, "Keeps Icon Renamed", "Garde", "Behaelt");
        incoming.IconUrl = string.Empty;

        await UpsertAsync( incoming);

        var row = await ReadAvailabilityAsync(9860056, TbcExpansionId);
        row!.IconUrl.Should().Be("https://cdn/icon.jpg");
        row.NameEn.Should().Be("Keeps Icon Renamed");
    }

    [Fact]
    public async Task UpsertAsync_SameSpellIdWithDifferentNamesOnTwoExpansions_NeverOverwritesEachOther()
    {
        // The core regression: syncing branch A (expansion 2), then branch B (expansion 12) which reuses the
        // same spell ID under another name, then branch A again must report zero renames the second time.
        const int spellId = 9860059;
        var forExpansionA = () => MakeAvailability(spellId, "Name From A", "Nom A", "Name A", TbcExpansionId);
        var forExpansionB = () => MakeAvailability(spellId, "Name From B", "Nom B", "Name B", ForeverExpansionId);

        var firstA = await UpsertAsync( forExpansionA());
        var firstB = await UpsertAsync( forExpansionB());
        var secondA = await UpsertAsync( forExpansionA());
        var secondB = await UpsertAsync( forExpansionB());

        firstA.Added.Should().ContainSingle();
        firstB.Added.Should().ContainSingle();
        firstB.Renamed.Should().BeEmpty();
        secondA.Added.Should().BeEmpty();
        secondA.Renamed.Should().BeEmpty();
        secondB.Added.Should().BeEmpty();
        secondB.Renamed.Should().BeEmpty();
        (await ReadAvailabilityAsync(spellId, TbcExpansionId))!.NameEn.Should().Be("Name From A");
        (await ReadAvailabilityAsync(spellId, ForeverExpansionId))!.NameEn.Should().Be("Name From B");
    }

    [Fact]
    public async Task UpsertAsync_MixedBatch_ReportsAddedAndRenamedSeparately()
    {
        await SeedAsync(db =>
        {
            AddSpell(db, 9860060, "Before", "Avant", "Vorher");
            AddSpell(db, 9860061, "Unchanged", "Inchange", "Unveraendert");
            return Task.CompletedTask;
        });

        var diff = await UpsertAsync(
            MakeAvailability(9860060, "After", "Apres", "Nachher"),
            MakeAvailability(9860061, "Unchanged", "Inchange", "Unveraendert"),
            MakeAvailability(9860062, "Fresh", "Frais", "Frisch"));

        diff.Added.Select(e => e.SpellId).Should().Equal(9860062);
        diff.Renamed.Select(e => (e.SpellId, e.PreviousNameEn, e.NameEn)).Should().Equal((9860060, "Before", "After"));
    }

    [Fact]
    public async Task UpsertAsync_NoRows_ReturnsAnEmptyDiff()
    {
        var diff = await UpsertAsync();

        diff.Added.Should().BeEmpty();
        diff.Renamed.Should().BeEmpty();
    }
}
