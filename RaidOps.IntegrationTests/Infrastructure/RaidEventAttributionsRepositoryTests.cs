using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids;
using RaidOps.Domain.Models.Raids.Attributions;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.IntegrationTests.Infrastructure;

/// <summary>
/// Integration tests for <see cref="RaidEventAttributionsRepository"/>. All Discord/guild IDs are
/// in the 983… range to avoid primary-key conflicts with other test classes.
/// </summary>
[Collection("Integration")]
public class RaidEventAttributionsRepositoryTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    /// <summary>Seeds a guild/branch with two seated characters and one name-slot cell, ready for <c>SetAsync</c>. Returns every ID the caller needs.</summary>
    private async Task<(int EventId, int DefinitionId, int CellId, int FirstCharacterId, int SecondCharacterId)> SeedEventWithTwoCharactersAsync(string discordId, string guildId)
    {
        int eventId, definitionId, cellId, firstCharacterId, secondCharacterId;

        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(discordId));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.UserGuilds.Add(TestDataBuilder.CreateUserGuild(discordId, guildId, isAdmin: true));
            return Task.CompletedTask;
        });

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var guildBranch = TestDataBuilder.CreateGuildBranch(guildId, branchId: 1);
            db.GuildBranches.Add(guildBranch);
            await db.SaveChangesAsync();

            var realm = TestDataBuilder.CreateRealm(branchId: 1, slug: $"realm-{guildId}");
            db.Realms.Add(realm);
            await db.SaveChangesAsync();

            var firstCharacter = TestDataBuilder.CreateCharacter(discordId, realm.Id, branchId: 1, classId: 1, isActive: true, bnetCharacterId: long.Parse(guildId), name: "FirstChar");
            var secondCharacter = TestDataBuilder.CreateCharacter(discordId, realm.Id, branchId: 1, classId: 2, isActive: true, bnetCharacterId: long.Parse(guildId) + 1, name: "SecondChar");
            db.Characters.AddRange(firstCharacter, secondCharacter);
            await db.SaveChangesAsync();
            firstCharacterId = firstCharacter.Id;
            secondCharacterId = secondCharacter.Id;

            var raidEvent = new RaidEvent
            {
                GuildId = guildId,
                GuildBranchId = guildBranch.Id,
                Name = "Repository Test Event",
                PublicationStatus = RaidPublicationStatus.Published,
                StartsAtUtc = DateTime.UtcNow.AddDays(1),
                GroupCount = 1,
                SlotsPerGroup = 5,
                CreatedByDiscordId = discordId,
                CreatedAt = DateTime.UtcNow,
            };
            db.RaidEvents.Add(raidEvent);
            await db.SaveChangesAsync();
            eventId = raidEvent.Id;

            var definition = new GuildAttributionDefinition
            {
                GuildId = guildId,
                Label = "Slot",
                SortOrder = 0,
                CreatedAt = DateTime.UtcNow,
                CreatedByDiscordId = discordId,
                Cells = [new AttributionDefinitionCell { CellIndex = 0, Kind = AttributionCellKind.NameSlot, SlotLabel = "Slot" }],
            };
            db.GuildAttributionDefinitions.Add(definition);
            await db.SaveChangesAsync();
            definitionId = definition.Id;
            cellId = definition.Cells.Single().Id;

            (await db.AttributionDefinitionCells.CountAsync(c => c.GuildAttributionDefinitionId == definitionId)).Should().Be(1);
        }

        return (eventId, definitionId, cellId, firstCharacterId, secondCharacterId);
    }

    [Fact]
    public async Task SetAsync_ExistingFillAtCoordinate_UpdatesCharacterInPlaceInsteadOfDuplicating()
    {
        const string discordId = "983000000000000001";
        const string guildId = "983000000000000001";
        var (eventId, definitionId, cellId, firstCharacterId, secondCharacterId) = await SeedEventWithTwoCharactersAsync(discordId, guildId);

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRaidEventAttributionsRepository>();

            await repo.SetAsync(eventId, cellId, definitionId, instanceIndex: 0, firstCharacterId, discordId);
            var firstAssignedAt = db.RaidEventAttributions.AsNoTracking().Single(a => a.RaidEventId == eventId && a.AttributionDefinitionCellId == cellId).AssignedAt;

            await Task.Delay(10);
            await repo.SetAsync(eventId, cellId, definitionId, instanceIndex: 0, secondCharacterId, discordId);

            var rows = db.RaidEventAttributions.AsNoTracking().Where(a => a.RaidEventId == eventId && a.AttributionDefinitionCellId == cellId).ToList();
            rows.Should().ContainSingle("the second SetAsync should update the existing row, not add a second one");
            rows[0].CharacterId.Should().Be(secondCharacterId);
            rows[0].AssignedAt.Should().BeAfter(firstAssignedAt);
        }
    }
}
