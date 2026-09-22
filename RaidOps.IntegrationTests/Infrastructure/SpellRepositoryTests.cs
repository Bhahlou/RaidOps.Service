using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.IntegrationTests.Infrastructure;

/// <summary>
/// Integration tests for <see cref="SpellRepository"/> — spell IDs are in the 9990… range, well
/// above any real Blizzard spell ID, to avoid colliding with the checked-in TBC spell seed data
/// <see cref="RaidOps.API.Seeding.SpellSeeder"/> loads on every test-host startup.
/// </summary>
[Collection("Integration")]
public class SpellRepositoryTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private const int TbcExpansionId = 2;

    private static Spell MakeSpell(int id, string nameEn, string nameFr, string nameDe) => new()
    {
        Id = id,
        ExpansionId = TbcExpansionId,
        NameEn = nameEn,
        NameFr = nameFr,
        NameDe = nameDe,
        IconUrl = "https://cdn/icon.jpg",
    };

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsSpell()
    {
        await SeedAsync(db =>
        {
            db.Spells.Add(MakeSpell(9990001, "Earth Shock", "Horion de terre", "Erdschock"));
            return Task.CompletedTask;
        });

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<ISpellRepository>();
            var result = await repo.GetByIdAsync(9990001);

            result.Should().NotBeNull();
            result!.NameEn.Should().Be("Earth Shock");
        }
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<ISpellRepository>();
            var result = await repo.GetByIdAsync(999999999);

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
            db.Spells.Add(MakeSpell(9990010, "Innervate", "Innervation", "ZqxvtestZweit"));
            db.Spells.Add(MakeSpell(9990011, "Renew", "Renouveau", "ZqxvtestAnfang"));
            return Task.CompletedTask;
        });

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<ISpellRepository>();
            var result = await repo.SearchAsync(TbcExpansionId, "Zqxvtest", "de", 10);

            // Both German names contain "Zqxvtest" — ordering by NameDe puts "Anfang" before "Zweit".
            result.Select(s => s.Id).Should().Equal(9990011, 9990010);
        }
    }

    [Fact]
    public async Task SearchAsync_UnknownLocale_FallsBackToEnglishNameAndOrder()
    {
        await SeedAsync(db =>
        {
            db.Spells.Add(MakeSpell(9990020, "ZqxvtestZzz Ability", "Zzz Capacité", "Zzz Fähigkeit"));
            db.Spells.Add(MakeSpell(9990021, "ZqxvtestAaa Ability", "Aaa Capacité", "Aaa Fähigkeit"));
            return Task.CompletedTask;
        });

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<ISpellRepository>();
            var result = await repo.SearchAsync(TbcExpansionId, "Zqxvtest", "es", 10);

            result.Select(s => s.Id).Should().Equal(9990021, 9990020);
        }
    }

    [Fact]
    public async Task InsertMissingAsync_AllAlreadyExist_ReturnsZeroAndDoesNotDuplicate()
    {
        await SeedAsync(db =>
        {
            db.Spells.Add(MakeSpell(9990030, "Existing", "Existant", "Vorhanden"));
            return Task.CompletedTask;
        });

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<ISpellRepository>();
            var inserted = await repo.InsertMissingAsync([MakeSpell(9990030, "Existing", "Existant", "Vorhanden")]);

            inserted.Should().Be(0);
            var count = db.Spells.Count(s => s.Id == 9990030);
            count.Should().Be(1);
        }
    }
}
