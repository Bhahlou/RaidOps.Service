using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
    public async Task ActivateAsync_ConcurrentActivationOfSamePair_SecondCallReactivatesInsteadOfThrowing()
    {
        const string guildId = "960000000000000005";
        const int branchId = 1;
        await SeedGuildAsync(guildId);

        var (scope1, db1) = CreateDbScope();
        var (scope2, _) = CreateDbScope();
        using (scope1)
        using (scope2)
        {
            var repo1 = scope1.ServiceProvider.GetRequiredService<IGuildBranchesRepository>();
            var repo2 = scope2.ServiceProvider.GetRequiredService<IGuildBranchesRepository>();

            // Keep repo1's insert uncommitted so repo2's own "does it exist" check still sees
            // nothing, forcing repo2 down the same insert path — reproducing the real race between
            // two concurrent activations of the same (guildId, branchId) pair.
            await using var tx1 = await db1.Database.BeginTransactionAsync();
            var winner = await repo1.ActivateAsync(guildId, branchId);

            var loserTask = Task.Run(() => repo2.ActivateAsync(guildId, branchId));
            // Give repo2's insert time to reach Postgres and block on repo1's still-uncommitted
            // unique-index entry before releasing it.
            await Task.Delay(300);
            await tx1.CommitAsync();

            var loser = await loserTask;

            winner.IsActive.Should().BeTrue();
            loser.IsActive.Should().BeTrue();
            loser.Id.Should().Be(winner.Id);
        }

        var (scope3, db3) = CreateDbScope();
        using (scope3)
        {
            db3.GuildBranches.Count(gb => gb.GuildId == guildId && gb.BranchId == branchId).Should().Be(1);
        }
    }

    [Fact]
    public async Task ActivateAsync_UnrelatedConstraintViolation_IsNotSwallowed()
    {
        const string guildId = "960000000000000006";
        await SeedGuildAsync(guildId);

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildBranchesRepository>();

            // No Branch with this ID exists — a foreign-key violation, not the unique-constraint
            // race ActivateAsync's catch is specifically meant to absorb, so it must propagate.
            var act = () => repo.ActivateAsync(guildId, branchId: 999999);

            await act.Should().ThrowAsync<DbUpdateException>();
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

    // ── UpdateSignupModeAsync ──────────────────────────────────────────────

    [Fact]
    public async Task UpdateSignupModeAsync_BranchNotFound_ReturnsFalse()
    {
        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildBranchesRepository>();
            var result = await repo.UpdateSignupModeAsync(guildBranchId: -1, SignupMode.Signup);

            result.Should().BeFalse();
        }
    }

    [Fact]
    public async Task UpdateSignupModeAsync_Success_PersistsSignupModeAndReturnsTrue()
    {
        const string guildId = "960000000000000004";
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
            var result = await repo.UpdateSignupModeAsync(branch.Id, SignupMode.Signup);

            result.Should().BeTrue();

            var updated = await db.GuildBranches.FindAsync(branch.Id);
            updated!.SignupMode.Should().Be(SignupMode.Signup);
        }
    }

    // ── GetCurrentExpansionIdAsync ───────────────────────────────────────────

    [Fact]
    public async Task GetCurrentExpansionIdAsync_KnownGuildBranch_ReturnsTheBranchsCurrentExpansion()
    {
        const string guildId = "986000000000000001";
        await SeedGuildAsync(guildId);
        // Branch 4 = Classic Anniversary (expansion 2), branch 5 = Forever (expansion 12): one guild, two branches.
        var anniversary = TestDataBuilder.CreateGuildBranch(guildId, branchId: 4);
        var forever = TestDataBuilder.CreateGuildBranch(guildId, branchId: 5);
        await SeedAsync(db =>
        {
            db.GuildBranches.AddRange(anniversary, forever);
            return Task.CompletedTask;
        });

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildBranchesRepository>();

            (await repo.GetCurrentExpansionIdAsync(guildId, anniversary.Id)).Should().Be(2);
            (await repo.GetCurrentExpansionIdAsync(guildId, forever.Id)).Should().Be(12);
        }
    }

    [Fact]
    public async Task GetCurrentExpansionIdAsync_GuildBranchOfAnotherGuild_ReturnsNull()
    {
        const string ownerGuildId = "986000000000000002";
        const string otherGuildId = "986000000000000003";
        await SeedGuildAsync(ownerGuildId);
        await SeedGuildAsync(otherGuildId);
        var branch = TestDataBuilder.CreateGuildBranch(ownerGuildId, branchId: 4);
        await SeedAsync(db =>
        {
            db.GuildBranches.Add(branch);
            return Task.CompletedTask;
        });

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildBranchesRepository>();

            (await repo.GetCurrentExpansionIdAsync(otherGuildId, branch.Id)).Should().BeNull();
        }
    }

    [Fact]
    public async Task GetCurrentExpansionIdAsync_UnknownGuildBranchId_ReturnsNull()
    {
        const string guildId = "986000000000000004";
        await SeedGuildAsync(guildId);

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildBranchesRepository>();

            (await repo.GetCurrentExpansionIdAsync(guildId, 999999999)).Should().BeNull();
        }
    }
}
