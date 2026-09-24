using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.IntegrationTests.Infrastructure;

/// <summary>
/// Integration tests for <see cref="BranchRepository"/>'s wago.tools sync support and the seeded
/// <c>Branch.WagoProductCode</c> values. Tests that need a branch in a state the seed data doesn't offer
/// flip the seeded row temporarily and always restore it — the integration collection runs sequentially,
/// so nothing else observes the intermediate state.
/// </summary>
[Collection("Integration")]
public class BranchRepositoryTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private const int RetailId = 1;
    private const int ClassicEraId = 2;
    private const int ClassicId = 3;
    private const int AnniversaryId = 4;
    private const int ForeverId = 5;

    private IBranchRepository ResolveRepo(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<IBranchRepository>();

    // ── Seed data ────────────────────────────────────────────────────────────

    [Fact]
    public async Task SeededBranches_HaveTheirWagoProductCodes()
    {
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var codes = await db.Branches.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.WagoProductCode);

            codes[RetailId].Should().Be("wow");
            codes[ClassicId].Should().Be("wow_classic");
            codes[AnniversaryId].Should().Be("wow_anniversary");
            codes[ForeverId].Should().Be("wow_classic_beta");
            codes[ClassicEraId].Should().BeNull();
        }
    }

    // ── GetActiveWagoTrackedAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetActiveWagoTrackedAsync_ReturnsActiveTrackedBranchesOrderedByIdWithTheirExpansion()
    {
        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var result = await ResolveRepo(scope).GetActiveWagoTrackedAsync();

            // Classic Era is inactive and untracked, so it is left out.
            result.Select(b => b.Id).Should().Equal(RetailId, ClassicId, AnniversaryId, ForeverId);
            result.Select(b => b.WagoProductCode).Should().Equal("wow", "wow_classic", "wow_anniversary", "wow_classic_beta");
            result.Should().OnlyContain(b => b.CurrentExpansion != null);
            result.Single(b => b.Id == ForeverId).CurrentExpansion.Id.Should().Be(12);
        }
    }

    [Fact]
    public async Task GetActiveWagoTrackedAsync_InactiveBranchWithAProductCode_IsExcluded()
    {
        await SetBranchAsync(ClassicEraId, isActive: false, productCode: "wow_classic_era");
        try
        {
            var (scope, _) = CreateDbScope();
            using (scope)
            {
                var result = await ResolveRepo(scope).GetActiveWagoTrackedAsync();

                result.Select(b => b.Id).Should().NotContain(ClassicEraId);
            }
        }
        finally
        {
            await SetBranchAsync(ClassicEraId, isActive: false, productCode: null);
        }
    }

    [Fact]
    public async Task GetActiveWagoTrackedAsync_ActiveBranchWithoutAProductCode_IsExcluded()
    {
        await SetBranchAsync(ClassicId, isActive: true, productCode: null);
        try
        {
            var (scope, _) = CreateDbScope();
            using (scope)
            {
                var result = await ResolveRepo(scope).GetActiveWagoTrackedAsync();

                result.Select(b => b.Id).Should().Equal(RetailId, AnniversaryId, ForeverId);
            }
        }
        finally
        {
            await SetBranchAsync(ClassicId, isActive: true, productCode: "wow_classic");
        }
    }

    [Fact]
    public async Task GetActiveWagoTrackedAsync_ActiveTrackedBranchTurnedInactive_IsExcluded()
    {
        await SetBranchAsync(ClassicId, isActive: false, productCode: "wow_classic");
        try
        {
            var (scope, _) = CreateDbScope();
            using (scope)
            {
                var result = await ResolveRepo(scope).GetActiveWagoTrackedAsync();

                result.Select(b => b.Id).Should().NotContain(ClassicId);
            }
        }
        finally
        {
            await SetBranchAsync(ClassicId, isActive: true, productCode: "wow_classic");
        }
    }

    // ── UpdateSyncStateAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task UpdateSyncStateAsync_PersistsBuildVersionAndDateOnThatBranchOnly()
    {
        var buildDate = new DateTime(2026, 9, 22, 14, 18, 1, DateTimeKind.Utc);
        try
        {
            var (scope, _) = CreateDbScope();
            using (scope)
                await ResolveRepo(scope).UpdateSyncStateAsync(ForeverId, "1.60.1.69977", buildDate);

            var (readScope, db) = CreateDbScope();
            using (readScope)
            {
                var forever = await db.Branches.AsNoTracking().SingleAsync(b => b.Id == ForeverId);
                forever.LastSyncedBuildVersion.Should().Be("1.60.1.69977");
                forever.LastSyncedBuildDate.Should().Be(buildDate);

                var retail = await db.Branches.AsNoTracking().SingleAsync(b => b.Id == RetailId);
                retail.LastSyncedBuildVersion.Should().BeNull();
                retail.LastSyncedBuildDate.Should().BeNull();
            }

            var (readScope2, _) = CreateDbScope();
            using (readScope2)
            {
                var tracked = await ResolveRepo(readScope2).GetActiveWagoTrackedAsync();
                tracked.Single(b => b.Id == ForeverId).LastSyncedBuildVersion.Should().Be("1.60.1.69977");
            }
        }
        finally
        {
            await SeedAsync(db => db.Branches.Where(b => b.Id == ForeverId)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.LastSyncedBuildVersion, (string?)null).SetProperty(b => b.LastSyncedBuildDate, (DateTime?)null)));
        }
    }

    [Fact]
    public async Task UpdateSyncStateAsync_UnknownBranch_Throws()
    {
        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var act = () => ResolveRepo(scope).UpdateSyncStateAsync(999999, "1.0.0.1", DateTime.UtcNow);

            await act.Should().ThrowAsync<InvalidOperationException>();
        }
    }

    private Task SetBranchAsync(int branchId, bool isActive, string? productCode)
        => SeedAsync(db => db.Branches.Where(b => b.Id == branchId)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.IsActive, isActive).SetProperty(b => b.WagoProductCode, productCode)));
}
