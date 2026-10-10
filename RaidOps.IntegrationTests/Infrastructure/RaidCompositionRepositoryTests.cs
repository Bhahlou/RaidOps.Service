using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.IntegrationTests.Infrastructure;

/// <summary>
/// Integration tests for <see cref="RaidCompositionRepository"/> members not reachable through
/// any controller endpoint yet (<see cref="IRaidCompositionRepository.GetAssignmentsForEventAsync"/>
/// isn't wired into a handler in this milestone). All IDs are in the 990… range to avoid
/// primary-key conflicts with other test classes.
/// </summary>
[Collection("Integration")]
public class RaidCompositionRepositoryTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    public async Task GetAssignmentsForEventAsync_ReturnsAssignmentsWithCharacterAndClassIncluded()
    {
        const string guildId = "990000000000000001";
        const string discordId = "990000000000000001";
        int eventId;
        int characterId;

        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(discordId));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.GuildBranches.Add(TestDataBuilder.CreateGuildBranch(guildId, branchId: 4));
            return Task.CompletedTask;
        });

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var realm = TestDataBuilder.CreateRealm(branchId: 4, slug: "realm-raidcomp-1");
            db.Realms.Add(realm);
            await db.SaveChangesAsync();

            var character = TestDataBuilder.CreateCharacter(discordId, realm.Id, branchId: 4, isActive: true, bnetCharacterId: 990001, name: "CompositionChar");
            db.Characters.Add(character);
            await db.SaveChangesAsync();
            characterId = character.Id;

            var raidEvent = new RaidEvent
            {
                GuildId = guildId,
                GuildBranchId = 4,
                Name = "Composition Test Event",
                StartsAtUtc = DateTime.UtcNow.AddDays(1),
                GroupCount = 2,
                SlotsPerGroup = 5,
                CreatedByDiscordId = discordId,
                CreatedAt = DateTime.UtcNow,
            };
            db.RaidEvents.Add(raidEvent);
            await db.SaveChangesAsync();
            eventId = raidEvent.Id;

            db.RaidSlotAssignments.Add(new RaidSlotAssignment
            {
                RaidEventId = eventId,
                GroupNumber = 1,
                SlotNumber = 1,
                CharacterId = characterId,
                SpecId = 62, // Arcane
                AssignedPlayerDiscordId = discordId,
                AssignedAt = DateTime.UtcNow,
                AssignedByDiscordId = discordId,
            });
            await db.SaveChangesAsync();
        }

        var (repoScope, _) = CreateDbScope();
        using (repoScope)
        {
            var repo = repoScope.ServiceProvider.GetRequiredService<IRaidCompositionRepository>();
            var assignments = await repo.GetAssignmentsForEventAsync(eventId);

            assignments.Should().ContainSingle();
            assignments[0].CharacterId.Should().Be(characterId);
            assignments[0].Character.Name.Should().Be("CompositionChar");
            assignments[0].Character.Class.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task GetAssignmentsForEventAsync_NoAssignments_ReturnsEmpty()
    {
        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRaidCompositionRepository>();
            var assignments = await repo.GetAssignmentsForEventAsync(raidEventId: -1);

            assignments.Should().BeEmpty();
        }
    }
}
