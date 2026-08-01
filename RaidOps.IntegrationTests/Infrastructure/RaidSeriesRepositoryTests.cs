using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.IntegrationTests.Infrastructure;

/// <summary>
/// Integration tests for <see cref="RaidSeriesRepository"/> — mainly
/// <see cref="IRaidSeriesRepository.GetByIdAsync"/>, which isn't reachable through any handler in
/// this milestone yet (every command/query handler goes through <c>GetByGuildBranchIdAsync</c>,
/// <c>AddAsync</c>, <c>UpdateAsync</c>, or <c>DeactivateAsync</c> instead).
/// </summary>
[Collection("Integration")]
public class RaidSeriesRepositoryTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private const int KarazhanZoneId = 1;

    [Fact]
    public async Task GetByIdAsync_ExistingSeriesOnMatchingBranch_ReturnsItWithZonesAndBranch()
    {
        const string guildId = "960000000000000010";
        var branch = TestDataBuilder.CreateGuildBranch(guildId, branchId: 4);
        var series = new RaidSeries
        {
            GuildId = guildId,
            Name = "Split 1",
            RecurrenceDayOfWeek = DayOfWeek.Wednesday,
            RecurrenceStartTimeLocal = new TimeOnly(20, 0),
            RecurrenceIntervalWeeks = 1,
            GroupCount = 2,
            SlotsPerGroup = 5,
            CreatedByDiscordId = "960000000000000010",
            CreatedAt = DateTime.UtcNow,
            DefaultZones = [new RaidSeriesZone { RaidZoneId = KarazhanZoneId }],
        };
        await SeedAsync(db =>
        {
            db.Guilds.Add(new Guild { Id = guildId, Name = "Series Repo Test Guild" });
            db.GuildBranches.Add(branch);
            return Task.CompletedTask;
        });
        series.GuildBranchId = branch.Id;
        await SeedAsync(db =>
        {
            db.RaidSeries.Add(series);
            return Task.CompletedTask;
        });

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRaidSeriesRepository>();
            var result = await repo.GetByIdAsync(series.Id, branch.Id);

            result.Should().NotBeNull();
            result!.Name.Should().Be("Split 1");
            result.DefaultZones.Should().ContainSingle(z => z.RaidZoneId == KarazhanZoneId);
            result.GuildBranch.Branch.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRaidSeriesRepository>();
            var result = await repo.GetByIdAsync(-1, guildBranchId: -1);

            result.Should().BeNull();
        }
    }

    [Fact]
    public async Task GetByIdAsync_SeriesBelongsToDifferentBranch_ReturnsNull()
    {
        const string guildId = "960000000000000011";
        var branch = TestDataBuilder.CreateGuildBranch(guildId, branchId: 4);
        var series = new RaidSeries
        {
            GuildId = guildId,
            Name = "Split 1",
            RecurrenceDayOfWeek = DayOfWeek.Wednesday,
            RecurrenceStartTimeLocal = new TimeOnly(20, 0),
            RecurrenceIntervalWeeks = 1,
            GroupCount = 2,
            SlotsPerGroup = 5,
            CreatedByDiscordId = "960000000000000011",
            CreatedAt = DateTime.UtcNow,
            DefaultZones = [new RaidSeriesZone { RaidZoneId = KarazhanZoneId }],
        };
        await SeedAsync(db =>
        {
            db.Guilds.Add(new Guild { Id = guildId, Name = "Series Repo Test Guild 2" });
            db.GuildBranches.Add(branch);
            return Task.CompletedTask;
        });
        series.GuildBranchId = branch.Id;
        await SeedAsync(db =>
        {
            db.RaidSeries.Add(series);
            return Task.CompletedTask;
        });

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRaidSeriesRepository>();
            var result = await repo.GetByIdAsync(series.Id, guildBranchId: branch.Id + 999);

            result.Should().BeNull();
        }
    }
}
