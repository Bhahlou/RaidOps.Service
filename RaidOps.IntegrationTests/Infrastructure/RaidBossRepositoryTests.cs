using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.IntegrationTests.Infrastructure;

/// <summary>
/// Integration tests for <see cref="RaidBossRepository"/> against the statically seeded
/// <see cref="RaidOps.Domain.Models.Raids.RaidBoss"/> reference table.
/// </summary>
[Collection("Integration")]
public class RaidBossRepositoryTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private const int HydrossBossId = 14; // Serpentshrine Cavern (RaidZoneId 4).
    private const int KarazhanZoneId = 1;
    private const int SscZoneId = 4;

    [Fact]
    public async Task GetByIdAsync_SeededBoss_ReturnsItWithZoneIncluded()
    {
        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRaidBossRepository>();
            var result = await repo.GetByIdAsync(HydrossBossId);

            result.Should().NotBeNull();
            result!.Name.Should().Be("Hydross the Unstable");
            result.RaidZoneId.Should().Be(SscZoneId);
            result.RaidZone.Should().NotBeNull();
            result.RaidZone.ShortCode.Should().Be("SSC");
        }
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRaidBossRepository>();
            var result = await repo.GetByIdAsync(999999);

            result.Should().BeNull();
        }
    }

    [Fact]
    public async Task GetForZonesAsync_SingleZone_ReturnsOnlyThatZonesBossesOrderedBySortOrder()
    {
        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRaidBossRepository>();
            var result = await repo.GetForZonesAsync([KarazhanZoneId]);

            result.Should().NotBeEmpty();
            result.Should().OnlyContain(b => b.RaidZoneId == KarazhanZoneId);
            result.Should().BeInAscendingOrder(b => b.SortOrder);
        }
    }

    [Fact]
    public async Task GetForZonesAsync_MultipleZones_ReturnsUnionAcrossThem()
    {
        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRaidBossRepository>();
            var result = await repo.GetForZonesAsync([KarazhanZoneId, SscZoneId]);

            result.Should().Contain(b => b.RaidZoneId == KarazhanZoneId);
            result.Should().Contain(b => b.Id == HydrossBossId && b.RaidZoneId == SscZoneId);
        }
    }

    [Fact]
    public async Task GetForZonesAsync_UnknownZone_ReturnsEmpty()
    {
        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRaidBossRepository>();
            var result = await repo.GetForZonesAsync([999999]);

            result.Should().BeEmpty();
        }
    }
}
