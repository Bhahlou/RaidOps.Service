using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.IntegrationTests.Infrastructure;

/// <summary>
/// Direct repository tests for <see cref="IGuildsRepository.UpsertRangeAsync"/>.
/// The HTTP layer doesn't exercise both branches because the Discord API stub
/// returns an empty guild list, leaving the foreach body uncovered.
/// </summary>
public class GuildsRepositoryTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    public async Task UpsertRange_NewGuild_InsertsIntoDatabase()
    {
        const string guildId = "800000000000000001";

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildsRepository>();
            await repo.UpsertRangeAsync([new Guild { Id = guildId, Name = "New Guild" }]);

            var inserted = await db.Guilds.FindAsync(guildId);
            inserted.Should().NotBeNull();
            inserted!.Name.Should().Be("New Guild");
        }
    }

    [Fact]
    public async Task UpsertRange_ExistingGuild_UpdatesNameAndIconHash()
    {
        const string guildId = "800000000000000002";
        await SeedAsync(db =>
        {
            db.Guilds.Add(new Guild { Id = guildId, Name = "Old Name", IconHash = "old-hash" });
            return Task.CompletedTask;
        });

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildsRepository>();
            await repo.UpsertRangeAsync([new Guild { Id = guildId, Name = "New Name", IconHash = "new-hash" }]);

            var updated = await db.Guilds.FindAsync(guildId);
            updated!.Name.Should().Be("New Name");
            updated.IconHash.Should().Be("new-hash");
        }
    }

    [Fact]
    public async Task UnregisterAsync_GuildNotFound_DoesNothing()
    {
        const string guildId = "800000000000000003";

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildsRepository>();
            await repo.UnregisterAsync(guildId);

            var guild = await db.Guilds.FindAsync(guildId);
            guild.Should().BeNull();
        }
    }

    [Fact]
    public async Task UnregisterAsync_ExistingGuild_SetsIsRegisteredFalse()
    {
        const string guildId = "800000000000000004";
        await SeedAsync(db =>
        {
            db.Guilds.Add(new Guild { Id = guildId, Name = "Registered Guild", IsRegistered = true });
            return Task.CompletedTask;
        });

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildsRepository>();
            await repo.UnregisterAsync(guildId);

            var guild = await db.Guilds.FindAsync(guildId);
            guild!.IsRegistered.Should().BeFalse();
        }
    }

    [Fact]
    public async Task UpdateSettingsAsync_GuildNotFound_ReturnsFalse()
    {
        const string guildId = "800000000000000005";

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildsRepository>();
            var result = await repo.UpdateSettingsAsync(guildId, "Europe/Paris", RosterMode.Open, null);

            result.Should().BeFalse();
        }
    }

    [Fact]
    public async Task UpdateSettingsAsync_DiscordRoleOnly_SetsMinRosterRoleId()
    {
        const string guildId = "800000000000000006";
        await SeedAsync(db =>
        {
            db.Guilds.Add(new Guild { Id = guildId, Name = "Guild", IsRegistered = true });
            return Task.CompletedTask;
        });

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildsRepository>();
            var result = await repo.UpdateSettingsAsync(guildId, "Europe/Paris", RosterMode.DiscordRoleOnly, "role-123");

            result.Should().BeTrue();
            var guild = await db.Guilds.FindAsync(guildId);
            guild!.MinRosterRoleId.Should().Be("role-123");
        }
    }

    [Fact]
    public async Task UpdateSettingsAsync_Open_SetsMinRosterRoleIdToNull()
    {
        const string guildId = "800000000000000007";
        await SeedAsync(db =>
        {
            db.Guilds.Add(new Guild { Id = guildId, Name = "Guild", IsRegistered = true, MinRosterRoleId = "role-abc" });
            return Task.CompletedTask;
        });

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildsRepository>();
            var result = await repo.UpdateSettingsAsync(guildId, "UTC", RosterMode.Open, "role-abc");

            result.Should().BeTrue();
            var guild = await db.Guilds.FindAsync(guildId);
            guild!.MinRosterRoleId.Should().BeNull();
        }
    }
}
