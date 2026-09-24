using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;
using RaidOps.Infrastructure.Persistence.Implementations.Repositories;

namespace RaidOps.IntegrationTests.Infrastructure;

/// <summary>
/// Integration tests for the chunked bulk path of <see cref="SpellRepository.UpsertAsync"/> (chunks of
/// 5,000 rows, each saved and evicted from the change tracker before the next). Every test owns a block
/// of 20,000 spell IDs in the 970…–976… range — well above any real Blizzard spell ID and unused by every
/// other test class — and deletes it when done so the shared database stays small.
/// </summary>
[Collection("Integration")]
public class SpellRepositoryBulkUpsertTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private const int ChunkSize = 5_000;
    private const int BlockSize = 20_000;
    private const int TbcExpansionId = 2;
    private const int ForeverExpansionId = 12;

    // One block per test, so tests never see each other's rows even though they share the database.
    private const int InsertBlock = 9_700_000;
    private const int RerunBlock = 9_720_000;
    private const int MixedBlock = 9_740_000;
    private const int BoundaryBlock = 9_760_000;

    private static SpellAvailability MakeRow(int spellId, string nameEn, int expansionId = TbcExpansionId, string iconUrl = "https://cdn/bulk.jpg") => new()
    {
        SpellId = spellId,
        ExpansionId = expansionId,
        NameEn = nameEn,
        NameFr = $"{nameEn} fr",
        NameDe = $"{nameEn} de",
        IconUrl = iconUrl,
    };

    private static string NameOf(int block, int index) => $"Zqxbulk {block}-{index}";

    /// <summary>A lazy sequence of <paramref name="count"/> rows, like the sync handler hands the repository.</summary>
    private static IEnumerable<SpellAvailability> LazyRows(int block, int count, Func<int, string>? nameOf = null)
        => Enumerable.Range(0, count).Select(i => MakeRow(block + i, (nameOf ?? (index => NameOf(block, index)))(i)));

    private async Task<SpellSyncDiff> UpsertAsync(IEnumerable<SpellAvailability> rows)
    {
        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<ISpellRepository>();
            return await repo.UpsertAsync(rows);
        }
    }

    private async Task<(int Spells, int Availabilities)> CountAsync(int block)
    {
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var spells = await db.Spells.CountAsync(s => s.Id >= block && s.Id < block + BlockSize);
            var availabilities = await db.SpellAvailabilities.CountAsync(a => a.SpellId >= block && a.SpellId < block + BlockSize);
            return (spells, availabilities);
        }
    }

    private async Task<SpellAvailability?> ReadAsync(int spellId, int expansionId)
    {
        var (scope, db) = CreateDbScope();
        using (scope)
            return await db.SpellAvailabilities.AsNoTracking().SingleOrDefaultAsync(a => a.SpellId == spellId && a.ExpansionId == expansionId);
    }

    private Task CleanUpAsync(int block)
        => SeedAsync(db => db.Spells.Where(s => s.Id >= block && s.Id < block + BlockSize).ExecuteDeleteAsync());

    [Fact]
    public async Task UpsertAsync_MoreThanOneChunkOfNewRows_InsertsEverythingAndReturnsOneMergedAddedDiff()
    {
        const int total = 2 * ChunkSize + 3;
        try
        {
            var diff = await UpsertAsync(LazyRows(InsertBlock, total));

            diff.Added.Should().HaveCount(total);
            diff.Added.Select(e => e.SpellId).Should().Equal(Enumerable.Range(InsertBlock, total));
            diff.Added.Should().OnlyContain(e => e.PreviousNameEn == null);
            diff.Added[0].NameEn.Should().Be(NameOf(InsertBlock, 0));
            diff.Added[^1].NameEn.Should().Be(NameOf(InsertBlock, total - 1));
            diff.Renamed.Should().BeEmpty();
            (await CountAsync(InsertBlock)).Should().Be((total, total));

            // First row of each chunk, and the very last one, all made it with their own content.
            foreach (var index in new[] { 0, ChunkSize - 1, ChunkSize, 2 * ChunkSize - 1, 2 * ChunkSize, total - 1 })
            {
                var row = await ReadAsync(InsertBlock + index, TbcExpansionId);
                row.Should().NotBeNull($"row {index} should have been inserted");
                row!.NameEn.Should().Be(NameOf(InsertBlock, index));
                row.NameFr.Should().Be($"{NameOf(InsertBlock, index)} fr");
                row.NameDe.Should().Be($"{NameOf(InsertBlock, index)} de");
                row.IconUrl.Should().Be("https://cdn/bulk.jpg");
            }
        }
        finally
        {
            await CleanUpAsync(InsertBlock);
        }
    }

    [Fact]
    public async Task UpsertAsync_RerunOverMultipleChunks_UnchangedRowsProduceNoDiffAndRenamesAreMergedAcrossChunks()
    {
        const int total = 2 * ChunkSize + 3;
        // Renames straddling every chunk boundary, plus the first and last rows.
        var renamedIndexes = new[] { 0, ChunkSize - 1, ChunkSize, 2 * ChunkSize - 1, 2 * ChunkSize, total - 1 };
        try
        {
            await UpsertAsync(LazyRows(RerunBlock, total));

            var unchanged = await UpsertAsync(LazyRows(RerunBlock, total));

            unchanged.Added.Should().BeEmpty();
            unchanged.Renamed.Should().BeEmpty();
            (await CountAsync(RerunBlock)).Should().Be((total, total));

            var renamed = await UpsertAsync(LazyRows(RerunBlock, total, i => renamedIndexes.Contains(i) ? $"Renamed {i}" : NameOf(RerunBlock, i)));

            renamed.Added.Should().BeEmpty();
            renamed.Renamed.Select(e => (e.SpellId, e.PreviousNameEn, e.NameEn)).Should().Equal(
                renamedIndexes.Select(i => (RerunBlock + i, (string?)NameOf(RerunBlock, i), $"Renamed {i}")));
            (await CountAsync(RerunBlock)).Should().Be((total, total));
            foreach (var index in renamedIndexes)
            {
                var row = await ReadAsync(RerunBlock + index, TbcExpansionId);
                row!.NameEn.Should().Be($"Renamed {index}");
                row.NameFr.Should().Be($"Renamed {index} fr");
            }
            (await ReadAsync(RerunBlock + 1, TbcExpansionId))!.NameEn.Should().Be(NameOf(RerunBlock, 1));
        }
        finally
        {
            await CleanUpAsync(RerunBlock);
        }
    }

    [Fact]
    public async Task UpsertAsync_ExistingAndNewRowsMixedAcrossAChunkBoundary_AddsOnlyTheNewOnesAndUpdatesTheExistingOnes()
    {
        // 6,000 rows already in the database; the upsert of 2 * ChunkSize + 3 rows then covers them in chunk 1
        // (all existing), chunk 2 (1,000 existing then 4,000 new — the mix straddles the chunk) and chunk 3 (all new).
        const int existing = 6_000;
        const int total = 2 * ChunkSize + 3;
        try
        {
            await SeedAsync(db =>
            {
                for (var i = 0; i < existing; i++)
                {
                    db.Spells.Add(new Spell { Id = MixedBlock + i });
                    db.SpellAvailabilities.Add(MakeRow(MixedBlock + i, NameOf(MixedBlock, i), iconUrl: "https://cdn/old.jpg"));
                }
                return Task.CompletedTask;
            });

            var diff = await UpsertAsync(LazyRows(MixedBlock, total, i => i % 1_000 == 0 ? $"Renamed {i}" : NameOf(MixedBlock, i)));

            diff.Added.Select(e => e.SpellId).Should().Equal(Enumerable.Range(MixedBlock + existing, total - existing));
            diff.Renamed.Select(e => e.SpellId).Should().Equal(Enumerable.Range(0, 6).Select(k => MixedBlock + k * 1_000));
            diff.Renamed.Should().OnlyContain(e => e.PreviousNameEn != null && e.PreviousNameEn.StartsWith("Zqxbulk") && e.NameEn.StartsWith("Renamed"));
            (await CountAsync(MixedBlock)).Should().Be((total, total));

            // Existing rows got refreshed (new icon since the incoming one is non-empty), new ones were inserted.
            (await ReadAsync(MixedBlock + 5_000, TbcExpansionId))!.NameEn.Should().Be("Renamed 5000");
            (await ReadAsync(MixedBlock + 5_999, TbcExpansionId))!.IconUrl.Should().Be("https://cdn/bulk.jpg");
            (await ReadAsync(MixedBlock + 6_001, TbcExpansionId))!.NameEn.Should().Be(NameOf(MixedBlock, 6_001));
            (await ReadAsync(MixedBlock + total - 1, TbcExpansionId))!.NameEn.Should().Be(NameOf(MixedBlock, total - 1));
        }
        finally
        {
            await CleanUpAsync(MixedBlock);
        }
    }

    [Fact]
    public async Task UpsertAsync_SameNewSpellOnTwoExpansionsAcrossAChunkBoundary_CreatesTheBareSpellOnlyOnce()
    {
        // Row ChunkSize - 1 (last of chunk 1) and row ChunkSize (first of chunk 2) share a spell ID on two
        // expansions: chunk 2 must recognise the Spell chunk 1 already saved instead of inserting it again.
        const int sharedSpellId = BoundaryBlock + ChunkSize - 1;
        try
        {
            var rows = LazyRows(BoundaryBlock, ChunkSize - 1)
                .Append(MakeRow(sharedSpellId, "Shared Across Chunks", TbcExpansionId))
                .Append(MakeRow(sharedSpellId, "Shared Across Chunks Forever", ForeverExpansionId))
                .Concat(LazyRows(BoundaryBlock + ChunkSize, 10));

            var diff = await UpsertAsync(rows);

            diff.Added.Should().HaveCount(ChunkSize - 1 + 2 + 10);
            diff.Renamed.Should().BeEmpty();
            var (scope, db) = CreateDbScope();
            using (scope)
                (await db.Spells.CountAsync(s => s.Id == sharedSpellId)).Should().Be(1);
            (await ReadAsync(sharedSpellId, TbcExpansionId))!.NameEn.Should().Be("Shared Across Chunks");
            (await ReadAsync(sharedSpellId, ForeverExpansionId))!.NameEn.Should().Be("Shared Across Chunks Forever");
        }
        finally
        {
            await CleanUpAsync(BoundaryBlock);
        }
    }

    [Fact]
    public async Task UpsertAsync_MultiChunkRunKeepsPerExpansionIsolationAndNeverBlanksAnExistingIcon()
    {
        // The second chunk holds: an existing TBC row re-sent with an EMPTY icon and a new English name, and
        // the same spell ID on Forever under another name. Neither may leak into the other, and the icon stays.
        const int spellId = BoundaryBlock + 10_000;
        try
        {
            await SeedAsync(db =>
            {
                db.Spells.Add(new Spell { Id = spellId });
                db.SpellAvailabilities.Add(MakeRow(spellId, "Tbc Original", TbcExpansionId, "https://cdn/tbc-original.jpg"));
                return Task.CompletedTask;
            });
            var rows = LazyRows(BoundaryBlock + 10_001, ChunkSize)
                .Append(MakeRow(spellId, "Tbc Renamed", TbcExpansionId, iconUrl: string.Empty))
                .Append(MakeRow(spellId, "Forever Fresh", ForeverExpansionId, "https://cdn/forever.jpg"));

            var diff = await UpsertAsync(rows);

            diff.Added.Should().HaveCount(ChunkSize + 1);
            diff.Added.Should().Contain(e => e.SpellId == spellId && e.NameEn == "Forever Fresh");
            var renamed = diff.Renamed.Should().ContainSingle().Subject;
            (renamed.SpellId, renamed.PreviousNameEn, renamed.NameEn).Should().Be((spellId, "Tbc Original", "Tbc Renamed"));
            var tbc = await ReadAsync(spellId, TbcExpansionId);
            tbc!.NameEn.Should().Be("Tbc Renamed");
            tbc.IconUrl.Should().Be("https://cdn/tbc-original.jpg");
            var forever = await ReadAsync(spellId, ForeverExpansionId);
            forever!.NameEn.Should().Be("Forever Fresh");
            forever.IconUrl.Should().Be("https://cdn/forever.jpg");
        }
        finally
        {
            await CleanUpAsync(BoundaryBlock);
        }
    }

    [Fact]
    public async Task UpsertAsync_ExactlyOneFullChunk_InsertsItAndDoesNotRunAnEmptySecondChunk()
    {
        try
        {
            var diff = await UpsertAsync(LazyRows(BoundaryBlock + 15_000, ChunkSize));

            diff.Added.Should().HaveCount(ChunkSize);
            diff.Renamed.Should().BeEmpty();
            (await CountAsync(BoundaryBlock)).Should().Be((ChunkSize, ChunkSize));
        }
        finally
        {
            await CleanUpAsync(BoundaryBlock);
        }
    }
}
