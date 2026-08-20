using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.IntegrationTests.Infrastructure;

/// <summary>Integration tests for <see cref="RaidSignupRepository"/> against a real database.</summary>
[Collection("Integration")]
public class RaidSignupRepositoryTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private async Task<(string GuildId, string DiscordId, int BranchId, int CharacterId, int EventId)> SeedEventAsync(string suffix)
    {
        var guildId = $"9900000000000009{suffix}";
        var discordId = guildId;
        var branch = TestDataBuilder.CreateGuildBranch(guildId, branchId: 4);
        await SeedAsync(db =>
        {
            db.Users.Add(TestDataBuilder.CreateUser(discordId));
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            db.GuildBranches.Add(branch);
            return Task.CompletedTask;
        });

        var (seedScope, seedDb) = CreateDbScope();
        int characterId;
        int eventId;
        using (seedScope)
        {
            var realm = TestDataBuilder.CreateRealm(branchId: 4, slug: $"realm-signup-{suffix}");
            seedDb.Realms.Add(realm);
            await seedDb.SaveChangesAsync();

            var character = TestDataBuilder.CreateCharacter(discordId, realm.Id, branchId: 4, isActive: true, bnetCharacterId: 991000 + int.Parse(suffix), name: $"SignupChar{suffix}");
            seedDb.Characters.Add(character);
            await seedDb.SaveChangesAsync();
            characterId = character.Id;

            var raidEvent = new RaidEvent
            {
                GuildId = guildId,
                GuildBranchId = branch.Id,
                Name = "Signup Repo Event",
                StartsAtUtc = DateTime.UtcNow.AddDays(1),
                GroupCount = 2,
                SlotsPerGroup = 5,
                SignupMode = SignupMode.Signup,
                CreatedByDiscordId = discordId,
                CreatedAt = DateTime.UtcNow,
            };
            seedDb.RaidEvents.Add(raidEvent);
            await seedDb.SaveChangesAsync();
            eventId = raidEvent.Id;
        }

        return (guildId, discordId, branch.Id, characterId, eventId);
    }

    // ── GetAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_NoResponse_ReturnsNull()
    {
        var (_, discordId, _, _, eventId) = await SeedEventAsync("1");

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRaidSignupRepository>();
            var result = await repo.GetAsync(eventId, discordId);

            result.Should().BeNull();
        }
    }

    [Fact]
    public async Task GetAsync_ExistingResponse_ReturnsIt()
    {
        var (_, discordId, _, characterId, eventId) = await SeedEventAsync("2");
        var respondedAt = DateTime.UtcNow;

        var (seedScope, seedDb) = CreateDbScope();
        using (seedScope)
        {
            seedDb.RaidSignups.Add(new RaidSignup { RaidEventId = eventId, UserDiscordId = discordId, Status = SignupStatus.Accepted, CharacterId = characterId, SpecId = 71, RespondedAtUtc = respondedAt });
            await seedDb.SaveChangesAsync();
        }

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRaidSignupRepository>();
            var result = await repo.GetAsync(eventId, discordId);

            result.Should().NotBeNull();
            result!.Status.Should().Be(SignupStatus.Accepted);
            result.CharacterId.Should().Be(characterId);
        }
    }

    // ── SetSignupAsync (upsert) ──────────────────────────────────────────────

    [Fact]
    public async Task SetSignupAsync_NoExistingResponse_Inserts()
    {
        var (_, discordId, _, characterId, eventId) = await SeedEventAsync("3");

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRaidSignupRepository>();
            await repo.SetSignupAsync(new RaidSignup { RaidEventId = eventId, UserDiscordId = discordId, Status = SignupStatus.Tentative, CharacterId = characterId, SpecId = 71, RespondedAtUtc = DateTime.UtcNow });

            var persisted = await db.RaidSignups.SingleAsync(s => s.RaidEventId == eventId && s.UserDiscordId == discordId);
            persisted.Status.Should().Be(SignupStatus.Tentative);
        }
    }

    [Fact]
    public async Task SetSignupAsync_ExistingResponse_UpdatesInPlaceRatherThanDuplicating()
    {
        var (_, discordId, _, characterId, eventId) = await SeedEventAsync("4");

        var (seedScope, seedDb) = CreateDbScope();
        using (seedScope)
        {
            seedDb.RaidSignups.Add(new RaidSignup { RaidEventId = eventId, UserDiscordId = discordId, Status = SignupStatus.Tentative, CharacterId = characterId, SpecId = 71, RespondedAtUtc = DateTime.UtcNow.AddHours(-1) });
            await seedDb.SaveChangesAsync();
        }

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRaidSignupRepository>();
            await repo.SetSignupAsync(new RaidSignup { RaidEventId = eventId, UserDiscordId = discordId, Status = SignupStatus.Declined, CharacterId = null, SpecId = null, RespondedAtUtc = DateTime.UtcNow });

            var all = await db.RaidSignups.Where(s => s.RaidEventId == eventId && s.UserDiscordId == discordId).ToListAsync();
            all.Should().ContainSingle();
            all[0].Status.Should().Be(SignupStatus.Declined);
            all[0].CharacterId.Should().BeNull();
        }
    }

    // ── GetForEventAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetForEventAsync_IncludesCharacterClassAndSpec()
    {
        var (_, discordId, _, characterId, eventId) = await SeedEventAsync("5");

        var (seedScope, seedDb) = CreateDbScope();
        using (seedScope)
        {
            seedDb.RaidSignups.Add(new RaidSignup { RaidEventId = eventId, UserDiscordId = discordId, Status = SignupStatus.Accepted, CharacterId = characterId, SpecId = 71, RespondedAtUtc = DateTime.UtcNow });
            await seedDb.SaveChangesAsync();
        }

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRaidSignupRepository>();
            var result = await repo.GetForEventAsync(eventId);

            result.Should().ContainSingle();
            result[0].Character.Should().NotBeNull();
            result[0].Character!.Class.Should().NotBeNull();
            result[0].Spec.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task GetForEventAsync_OnlyReturnsResponsesForTheGivenEvent()
    {
        var (_, discordId, branchId, characterId, eventId) = await SeedEventAsync("6");

        var (seedScope, seedDb) = CreateDbScope();
        int otherEventId;
        using (seedScope)
        {
            var otherEvent = new RaidEvent
            {
                GuildId = seedDb.RaidEvents.First(e => e.Id == eventId).GuildId,
                GuildBranchId = branchId,
                Name = "Other Event",
                StartsAtUtc = DateTime.UtcNow.AddDays(2),
                GroupCount = 2,
                SlotsPerGroup = 5,
                SignupMode = SignupMode.Signup,
                CreatedByDiscordId = discordId,
                CreatedAt = DateTime.UtcNow,
            };
            seedDb.RaidEvents.Add(otherEvent);
            await seedDb.SaveChangesAsync();
            otherEventId = otherEvent.Id;

            seedDb.RaidSignups.AddRange(
                new RaidSignup { RaidEventId = eventId, UserDiscordId = discordId, Status = SignupStatus.Accepted, CharacterId = characterId, SpecId = 71, RespondedAtUtc = DateTime.UtcNow },
                new RaidSignup { RaidEventId = otherEventId, UserDiscordId = discordId, Status = SignupStatus.Declined, RespondedAtUtc = DateTime.UtcNow });
            await seedDb.SaveChangesAsync();
        }

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRaidSignupRepository>();
            var result = await repo.GetForEventAsync(eventId);

            result.Should().ContainSingle(s => s.RaidEventId == eventId);
        }
    }

    // ── GetForEventsAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetForEventsAsync_ReturnsResponsesAcrossAllGivenEvents()
    {
        var (guildId, discordId, branchId, characterId, eventId) = await SeedEventAsync("7");

        var (seedScope, seedDb) = CreateDbScope();
        int otherEventId;
        using (seedScope)
        {
            var otherEvent = new RaidEvent
            {
                GuildId = guildId,
                GuildBranchId = branchId,
                Name = "Other Event",
                StartsAtUtc = DateTime.UtcNow.AddDays(2),
                GroupCount = 2,
                SlotsPerGroup = 5,
                SignupMode = SignupMode.Signup,
                CreatedByDiscordId = discordId,
                CreatedAt = DateTime.UtcNow,
            };
            seedDb.RaidEvents.Add(otherEvent);
            await seedDb.SaveChangesAsync();
            otherEventId = otherEvent.Id;

            seedDb.RaidSignups.AddRange(
                new RaidSignup { RaidEventId = eventId, UserDiscordId = discordId, Status = SignupStatus.Accepted, CharacterId = characterId, SpecId = 71, RespondedAtUtc = DateTime.UtcNow },
                new RaidSignup { RaidEventId = otherEventId, UserDiscordId = discordId, Status = SignupStatus.Declined, RespondedAtUtc = DateTime.UtcNow });
            await seedDb.SaveChangesAsync();
        }

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRaidSignupRepository>();
            var result = await repo.GetForEventsAsync([eventId, otherEventId]);

            result.Should().HaveCount(2);
        }
    }
}
