using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace RaidOps.IntegrationTests.Infrastructure;

/// <summary>
/// Verifies the static reference tables (<c>HasData</c> seeds in <c>RaidOpsDbContext</c>) are
/// actually present in the migrated database.
/// </summary>
[Collection("Integration")]
public class StaticSeedDataTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Expansions_AreSeeded()
    {
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var count = await db.Expansions.CountAsync();
            count.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task Races_AreSeeded()
    {
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var count = await db.Races.CountAsync();
            count.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task WowClasses_AreSeeded()
    {
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var count = await db.WowClasses.CountAsync();
            count.Should().BeGreaterThan(0);
        }
    }
}
