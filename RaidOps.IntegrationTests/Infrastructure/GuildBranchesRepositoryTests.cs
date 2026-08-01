using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.IntegrationTests.Infrastructure;

[Collection("Integration")]
public class GuildBranchesRepositoryTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private async Task SeedGuildAsync(string guildId) =>
        await SeedAsync(db =>
        {
            db.Guilds.Add(new Guild { Id = guildId, Name = "Guild" });
            return Task.CompletedTask;
        });

    [Fact]
    public async Task ActivateAsync_PreviouslyDeactivatedBranch_ReactivatesInPlace()
    {
        const string guildId = "960000000000000001";
        await SeedGuildAsync(guildId);
        await SeedAsync(db =>
        {
            db.GuildBranches.Add(TestDataBuilder.CreateGuildBranch(
                guildId, rosterMode: RosterMode.DiscordRoleOnly, rosterRoleIds: ["role-1"], isActive: false));
            return Task.CompletedTask;
        });

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildBranchesRepository>();
            var result = await repo.ActivateAsync(guildId, branchId: 1);

            result.IsActive.Should().BeTrue();
            result.RosterMode.Should().Be(RosterMode.DiscordRoleOnly);
            result.RosterRoleIds.Should().ContainSingle("role-1");

            var rows = db.GuildBranches.Where(gb => gb.GuildId == guildId).ToList();
            rows.Should().ContainSingle();
        }
    }

    [Fact]
    public async Task DeactivateAsync_BranchNotFound_ReturnsFalse()
    {
        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildBranchesRepository>();
            var result = await repo.DeactivateAsync(guildBranchId: -1);

            result.Should().BeFalse();
        }
    }

    [Fact]
    public async Task UpdateRosterSettingsAsync_BranchNotFound_ReturnsFalse()
    {
        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildBranchesRepository>();
            var result = await repo.UpdateRosterSettingsAsync(guildBranchId: -1, RosterMode.Open, rosterRoleIds: [], officerRoleIds: []);

            result.Should().BeFalse();
        }
    }

    [Fact]
    public async Task UpdateRosterSettingsAsync_ModeNotDiscordRoleOnly_ClearsRosterRoleIds()
    {
        const string guildId = "960000000000000002";
        await SeedGuildAsync(guildId);
        var branch = TestDataBuilder.CreateGuildBranch(
            guildId, rosterMode: RosterMode.DiscordRoleOnly, rosterRoleIds: ["role-1"]);
        await SeedAsync(db =>
        {
            db.GuildBranches.Add(branch);
            return Task.CompletedTask;
        });

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildBranchesRepository>();
            var result = await repo.UpdateRosterSettingsAsync(branch.Id, RosterMode.Open, rosterRoleIds: ["role-1"], officerRoleIds: []);

            result.Should().BeTrue();

            var updated = await db.GuildBranches.FindAsync(branch.Id);
            updated!.RosterMode.Should().Be(RosterMode.Open);
            updated.RosterRoleIds.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task UpdateRegionAsync_BranchNotFound_ReturnsFalse()
    {
        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildBranchesRepository>();
            var result = await repo.UpdateRegionAsync(guildBranchId: -1, region: "eu");

            result.Should().BeFalse();
        }
    }

    [Fact]
    public async Task UpdateRegionAsync_Success_PersistsRegionAndReturnsTrue()
    {
        const string guildId = "960000000000000003";
        await SeedGuildAsync(guildId);
        var branch = TestDataBuilder.CreateGuildBranch(guildId);
        await SeedAsync(db =>
        {
            db.GuildBranches.Add(branch);
            return Task.CompletedTask;
        });

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildBranchesRepository>();
            var result = await repo.UpdateRegionAsync(branch.Id, "eu");

            result.Should().BeTrue();

            var updated = await db.GuildBranches.FindAsync(branch.Id);
            updated!.Region.Should().Be("eu");
        }
    }
}
