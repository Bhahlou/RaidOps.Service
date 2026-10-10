using RaidOps.Domain.Models.Reference;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids.Attributions;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.IntegrationTests.Infrastructure;

/// <summary>
/// Integration tests for <see cref="GuildAttributionDefinitionsRepository"/> — in particular the
/// section-aware insertion-position logic in <see cref="GuildAttributionDefinitionsRepository.AddAsync"/>
/// and the bulk <see cref="GuildAttributionDefinitionsRepository.SetSectionIconAsync"/> update, neither
/// of which is exercised end-to-end by the controller tests' happy-path scenarios. All guild IDs are
/// in the 984… range to avoid primary-key conflicts with other test classes.
/// </summary>
[Collection("Integration")]
public class GuildAttributionDefinitionsRepositoryTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private const string CreatedByDiscordId = "984000000000000099";
    private const int HydrossBossId = 14; // Seeded boss of RaidZoneId 4 (Serpentshrine Cavern).

    private static GuildAttributionDefinition MakeDefinition(string guildId, int guildBranchId, string label, string? section, int? raidBossId = null) => new()
    {
        GuildId = guildId,
        GuildBranchId = guildBranchId,
        RaidBossId = raidBossId,
        Label = label,
        Section = section,
        CreatedAt = DateTime.UtcNow,
        CreatedByDiscordId = CreatedByDiscordId,
        Cells = [new AttributionDefinitionCell { CellIndex = 0, Kind = AttributionCellKind.NameSlot }],
    };

    /// <summary>Seeds a registered guild with one active guild branch (the FK every definition now hangs off) and returns the branch's surrogate ID.</summary>
    private async Task<int> SeedGuildAsync(string guildId)
    {
        await SeedAsync(db =>
        {
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            return Task.CompletedTask;
        });

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var guildBranch = TestDataBuilder.CreateGuildBranch(guildId);
            db.GuildBranches.Add(guildBranch);
            await db.SaveChangesAsync();
            return guildBranch.Id;
        }
    }

    [Fact]
    public async Task AddAsync_NoExistingSection_AppendsAtTheEnd()
    {
        const string guildId = "984000000000000001";
        var branchId = await SeedGuildAsync(guildId);
        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildAttributionDefinitionsRepository>();
            await repo.AddAsync(MakeDefinition(guildId, branchId, "Innervate", "Personals"));
            var added = await repo.AddAsync(MakeDefinition(guildId, branchId, "Tank swap", "Cooldowns"));

            added.SortOrder.Should().Be(1);
        }
    }

    [Fact]
    public async Task AddAsync_JoiningAnExistingSection_InsertsRightAfterThatSectionsLastRow()
    {
        const string guildId = "984000000000000002";
        var branchId = await SeedGuildAsync(guildId);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildAttributionDefinitionsRepository>();
            // Personals, Personals, Cooldowns — then a new "Personals" row must land at index 2
            // (right after the second Personals row), pushing "Cooldowns" down to index 3, not
            // appended at index 3 splitting the Personals rows into two groups.
            await repo.AddAsync(MakeDefinition(guildId, branchId, "Innervate", "Personals"));
            await repo.AddAsync(MakeDefinition(guildId, branchId, "PW: Shield", "Personals"));
            await repo.AddAsync(MakeDefinition(guildId, branchId, "Tank swap", "Cooldowns"));

            var added = await repo.AddAsync(MakeDefinition(guildId, branchId, "Fear ward", "Personals"));

            added.SortOrder.Should().Be(2);
            var all = await db.GuildAttributionDefinitions.Where(d => d.GuildId == guildId).OrderBy(d => d.SortOrder).ToListAsync();
            all.Select(d => d.Label).Should().Equal("Innervate", "PW: Shield", "Fear ward", "Tank swap");
            all.Select(d => d.SortOrder).Should().Equal(0, 1, 2, 3);
        }
    }

    [Fact]
    public async Task AddAsync_UngroupedRow_AlwaysAppendsAtTheEnd()
    {
        const string guildId = "984000000000000003";
        var branchId = await SeedGuildAsync(guildId);
        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildAttributionDefinitionsRepository>();
            await repo.AddAsync(MakeDefinition(guildId, branchId, "Innervate", "Personals"));
            var added = await repo.AddAsync(MakeDefinition(guildId, branchId, "Misc row", section: null));

            added.SortOrder.Should().Be(1);
        }
    }

    [Fact]
    public async Task AddAsync_GeneralAndBossScopesAreNumberedIndependently()
    {
        const string guildId = "984000000000000004";
        var branchId = await SeedGuildAsync(guildId);
        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildAttributionDefinitionsRepository>();
            await repo.AddAsync(MakeDefinition(guildId, branchId, "General row 1", "Personals"));
            await repo.AddAsync(MakeDefinition(guildId, branchId, "General row 2", "Personals"));

            // A boss-scoped row joining a same-named "Interrupts" section starts its own count at 0
            // — it must not be pushed down by the two unrelated General rows above.
            var bossRow = await repo.AddAsync(MakeDefinition(guildId, branchId, "Interrupt 1", "Interrupts", HydrossBossId));

            bossRow.SortOrder.Should().Be(0);
        }
    }

    [Fact]
    public async Task SetSectionIconAsync_UpdatesEveryRowSharingTheSectionInThatScopeOnly()
    {
        const string guildId = "984000000000000005";
        var branchId = await SeedGuildAsync(guildId);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildAttributionDefinitionsRepository>();
            await repo.AddAsync(MakeDefinition(guildId, branchId, "Innervate", "Personals"));
            await repo.AddAsync(MakeDefinition(guildId, branchId, "PW: Shield", "Personals"));
            // Same section name but scoped to a boss — must NOT be touched by the General update below.
            await repo.AddAsync(MakeDefinition(guildId, branchId, "Interrupt 1", "Personals", HydrossBossId));

            var updated = await repo.SetSectionIconAsync(guildId, branchId, null, "Personals", new SectionIconFields(AttributionIconSource.RaidMarker, null, RaidMarkerIcon.Skull, null));

            updated.Should().Be(2);
            // ExecuteUpdateAsync is a raw bulk SQL update — it never refreshes this context's
            // change tracker, so the already-tracked entities from the AddAsync calls above would
            // otherwise come back stale here.
            var rows = await db.GuildAttributionDefinitions.AsNoTracking().Where(d => d.GuildId == guildId).ToListAsync();
            rows.Where(d => d.RaidBossId == null).Should().OnlyContain(d => d.SectionIconSource == AttributionIconSource.RaidMarker && d.SectionRaidMarker == RaidMarkerIcon.Skull);
            var bossRow = rows.Should().ContainSingle(d => d.RaidBossId == HydrossBossId).Subject;
            bossRow.SectionIconSource.Should().Be(AttributionIconSource.None);
            bossRow.SectionRaidMarker.Should().BeNull();
        }
    }

    [Fact]
    public async Task SetSectionIconAsync_NoRowUsesThatSection_ReturnsZero()
    {
        const string guildId = "984000000000000006";
        const int branchId = 999999;
        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildAttributionDefinitionsRepository>();

            var updated = await repo.SetSectionIconAsync(guildId, branchId, null, "No such section", new SectionIconFields(AttributionIconSource.RaidMarker, null, RaidMarkerIcon.Skull, null));

            updated.Should().Be(0);
        }
    }

    // ── Guild-branch isolation ───────────────────────────────────────────────
    // Branch 4 (Classic Anniversary, expansion 2) and branch 5 (Forever, expansion 12) let one guild run
    // two independent templates. Guild IDs are 986000000000001xx; spell IDs 98601xx.

    private const int AnniversaryBranchRef = 4;
    private const int ForeverBranchRef = 5;

    /// <summary>Seeds a registered guild running the given wow branches and returns the guild-branch surrogate IDs in the same order.</summary>
    private async Task<int[]> SeedGuildWithBranchesAsync(string guildId, params int[] wowBranchIds)
    {
        await SeedAsync(db =>
        {
            db.Guilds.Add(TestDataBuilder.CreateGuild(guildId, isRegistered: true));
            return Task.CompletedTask;
        });

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var guildBranches = wowBranchIds.Select(id => TestDataBuilder.CreateGuildBranch(guildId, branchId: id)).ToList();
            db.GuildBranches.AddRange(guildBranches);
            await db.SaveChangesAsync();
            return guildBranches.Select(gb => gb.Id).ToArray();
        }
    }

    private async Task<List<GuildAttributionDefinition>> ReadAllAsync(string guildId)
    {
        var (scope, db) = CreateDbScope();
        using (scope)
            return await db.GuildAttributionDefinitions.AsNoTracking().Include(d => d.Cells).Where(d => d.GuildId == guildId).OrderBy(d => d.Id).ToListAsync();
    }

    private async Task<GuildAttributionDefinition> AddDefinitionAsync(string guildId, int branchId, string label, string? section, int? raidBossId = null)
    {
        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildAttributionDefinitionsRepository>();
            return await repo.AddAsync(MakeDefinition(guildId, branchId, label, section, raidBossId));
        }
    }

    [Fact]
    public async Task GetForBranchAsync_ReturnsOnlyThatBranchsRowsInTheRequestedScopeOrderedBySortOrder()
    {
        const string guildId = "986000000000000101";
        const string otherGuildId = "986000000000000102";
        var branches = await SeedGuildWithBranchesAsync(guildId, AnniversaryBranchRef, ForeverBranchRef);
        var otherGuildBranches = await SeedGuildWithBranchesAsync(otherGuildId, AnniversaryBranchRef);
        await AddDefinitionAsync(guildId, branches[0], "A first", null);
        await AddDefinitionAsync(guildId, branches[0], "A second", null);
        await AddDefinitionAsync(guildId, branches[0], "A boss row", null, HydrossBossId);
        await AddDefinitionAsync(guildId, branches[1], "B only", null);
        await AddDefinitionAsync(otherGuildId, otherGuildBranches[0], "Other guild", null);

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildAttributionDefinitionsRepository>();

            (await repo.GetForBranchAsync(guildId, branches[0], null)).Select(d => d.Label).Should().Equal("A first", "A second");
            (await repo.GetForBranchAsync(guildId, branches[0], HydrossBossId)).Select(d => d.Label).Should().Equal("A boss row");
            (await repo.GetForBranchAsync(guildId, branches[1], null)).Select(d => d.Label).Should().Equal("B only");
            (await repo.GetForBranchAsync(otherGuildId, otherGuildBranches[0], null)).Select(d => d.Label).Should().Equal("Other guild");
            // A guild-branch ID paired with the wrong guild finds nothing.
            (await repo.GetForBranchAsync(otherGuildId, branches[0], null)).Should().BeEmpty();
        }
    }

    [Fact]
    public async Task GetForBranchAsync_LoadsCellsInIndexOrderAndTheSpellsAvailabilities()
    {
        const string guildId = "986000000000000103";
        const int spellId = 9860101;
        var branches = await SeedGuildWithBranchesAsync(guildId, AnniversaryBranchRef);
        await SeedAsync(db =>
        {
            db.Spells.Add(new Spell { Id = spellId });
            db.SpellAvailabilities.Add(new SpellAvailability { SpellId = spellId, ExpansionId = 2, NameEn = "Bloodlust", NameFr = "Furie", NameDe = "Kampfrausch", IconUrl = "https://cdn/tbc.jpg" });
            db.SpellAvailabilities.Add(new SpellAvailability { SpellId = spellId, ExpansionId = 12, NameEn = "Bloodlust", NameFr = "Furie", NameDe = "Kampfrausch", IconUrl = "https://cdn/forever.jpg" });
            db.GuildAttributionDefinitions.Add(new GuildAttributionDefinition
            {
                GuildId = guildId,
                GuildBranchId = branches[0],
                Label = "Bloodlust",
                Section = "Cooldowns",
                SectionIconSource = AttributionIconSource.Spell,
                SectionSpellId = spellId,
                CreatedAt = DateTime.UtcNow,
                CreatedByDiscordId = CreatedByDiscordId,
                Cells =
                [
                    new AttributionDefinitionCell { CellIndex = 1, Kind = AttributionCellKind.NameSlot, SlotLabel = "Second" },
                    new AttributionDefinitionCell { CellIndex = 0, Kind = AttributionCellKind.Icon, IconSource = AttributionIconSource.Spell, SpellId = spellId },
                ],
            });
            return Task.CompletedTask;
        });

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildAttributionDefinitionsRepository>();

            var definition = (await repo.GetForBranchAsync(guildId, branches[0], null)).Should().ContainSingle().Subject;

            definition.Cells.Select(c => c.CellIndex).Should().Equal(0, 1);
            definition.Cells.First().Spell!.Availabilities.Select(a => a.ExpansionId).Should().BeEquivalentTo([2, 12]);
            definition.SectionSpell!.Availabilities.Should().HaveCount(2);
        }
    }

    [Fact]
    public async Task GetByIdAsync_LoadsCellsAndTheSpellsAvailabilities_AndUnknownIdReturnsNull()
    {
        const string guildId = "986000000000000104";
        const int spellId = 9860102;
        var branches = await SeedGuildWithBranchesAsync(guildId, AnniversaryBranchRef);
        var definition = MakeDefinition(guildId, branches[0], "By id", "Personals");
        definition.Cells = [new AttributionDefinitionCell { CellIndex = 0, Kind = AttributionCellKind.Icon, IconSource = AttributionIconSource.Spell, SpellId = spellId }];
        await SeedAsync(db =>
        {
            db.Spells.Add(new Spell { Id = spellId });
            db.SpellAvailabilities.Add(new SpellAvailability { SpellId = spellId, ExpansionId = 2, NameEn = "n", NameFr = "n", NameDe = "n", IconUrl = "https://cdn/i.jpg" });
            db.GuildAttributionDefinitions.Add(definition);
            return Task.CompletedTask;
        });

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildAttributionDefinitionsRepository>();

            var loaded = await repo.GetByIdAsync(definition.Id);

            loaded.Should().NotBeNull();
            loaded!.GuildBranchId.Should().Be(branches[0]);
            loaded.Cells.Single().GuildAttributionDefinitionId.Should().Be(definition.Id);
            loaded.Cells.Single().Spell!.Availabilities.Single().IconUrl.Should().Be("https://cdn/i.jpg");
            (await repo.GetByIdAsync(int.MaxValue)).Should().BeNull();
        }
    }

    [Fact]
    public async Task AddAsync_TwoBranchesOfTheSameGuild_AreNumberedIndependently()
    {
        const string guildId = "986000000000000105";
        var branches = await SeedGuildWithBranchesAsync(guildId, AnniversaryBranchRef, ForeverBranchRef);
        await AddDefinitionAsync(guildId, branches[0], "A1", "Personals");
        await AddDefinitionAsync(guildId, branches[0], "A2", "Personals");
        await AddDefinitionAsync(guildId, branches[0], "A3", "Cooldowns");

        var firstOnB = await AddDefinitionAsync(guildId, branches[1], "B1", "Cooldowns");
        var secondOnB = await AddDefinitionAsync(guildId, branches[1], "B2", "Personals");

        firstOnB.SortOrder.Should().Be(0);
        secondOnB.SortOrder.Should().Be(1);
        var all = await ReadAllAsync(guildId);
        all.Where(d => d.GuildBranchId == branches[0]).OrderBy(d => d.SortOrder).Select(d => d.Label).Should().Equal("A1", "A2", "A3");
        all.Where(d => d.GuildBranchId == branches[1]).Should().OnlyContain(d => d.GuildBranchId == branches[1]);
    }

    [Fact]
    public async Task UpdateAsync_OnItsOwnBranch_UpdatesFieldsAndReplacesCells()
    {
        const string guildId = "986000000000000106";
        var branches = await SeedGuildWithBranchesAsync(guildId, AnniversaryBranchRef);
        var original = await AddDefinitionAsync(guildId, branches[0], "Before", "Old section");

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildAttributionDefinitionsRepository>();

            var updated = await repo.UpdateAsync(new GuildAttributionDefinition
            {
                Id = original.Id,
                GuildId = guildId,
                GuildBranchId = branches[0],
                Label = "After",
                Section = "New section",
                IsRepeatable = true,
                Cells =
                [
                    new AttributionDefinitionCell { CellIndex = 0, Kind = AttributionCellKind.NameSlot, SlotLabel = "One" },
                    new AttributionDefinitionCell { CellIndex = 1, Kind = AttributionCellKind.NameSlot, SlotLabel = "Two" },
                ],
            }, guildId, branches[0]);

            updated.Should().BeTrue();
        }

        var row = (await ReadAllAsync(guildId)).Single();
        row.Label.Should().Be("After");
        row.Section.Should().Be("New section");
        row.IsRepeatable.Should().BeTrue();
        row.Cells.Select(c => c.SlotLabel).Should().BeEquivalentTo("One", "Two");
    }

    [Fact]
    public async Task UpdateAsync_FromAnotherBranchOrGuild_ReturnsFalseAndChangesNothing()
    {
        const string guildId = "986000000000000107";
        const string otherGuildId = "986000000000000108";
        var branches = await SeedGuildWithBranchesAsync(guildId, AnniversaryBranchRef, ForeverBranchRef);
        await SeedGuildWithBranchesAsync(otherGuildId, AnniversaryBranchRef);
        var original = await AddDefinitionAsync(guildId, branches[0], "Untouchable", "Section");

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildAttributionDefinitionsRepository>();
            GuildAttributionDefinition Replacement(string guild, int branch) => new()
            {
                Id = original.Id, GuildId = guild, GuildBranchId = branch, Label = "Hijacked", Section = "Hijacked",
                Cells = [new AttributionDefinitionCell { CellIndex = 0, Kind = AttributionCellKind.NameSlot }],
            };

            (await repo.UpdateAsync(Replacement(guildId, branches[1]), guildId, branches[1])).Should().BeFalse();
            (await repo.UpdateAsync(Replacement(otherGuildId, branches[0]), otherGuildId, branches[0])).Should().BeFalse();
        }

        var row = (await ReadAllAsync(guildId)).Single();
        row.Label.Should().Be("Untouchable");
        row.Section.Should().Be("Section");
    }

    [Fact]
    public async Task DeleteAsync_OnItsOwnBranch_DeletesTheRowAndItsCells()
    {
        const string guildId = "986000000000000109";
        var branches = await SeedGuildWithBranchesAsync(guildId, AnniversaryBranchRef);
        var definition = await AddDefinitionAsync(guildId, branches[0], "Doomed", "Section");

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildAttributionDefinitionsRepository>();

            (await repo.DeleteAsync(definition.Id, guildId, branches[0])).Should().BeTrue();
            (await db.GuildAttributionDefinitions.CountAsync(d => d.Id == definition.Id)).Should().Be(0);
            (await db.Set<AttributionDefinitionCell>().CountAsync(c => c.GuildAttributionDefinitionId == definition.Id)).Should().Be(0);
        }
    }

    [Fact]
    public async Task DeleteAsync_FromAnotherBranchOrGuild_ReturnsFalseAndKeepsTheRow()
    {
        const string guildId = "986000000000000110";
        const string otherGuildId = "986000000000000111";
        var branches = await SeedGuildWithBranchesAsync(guildId, AnniversaryBranchRef, ForeverBranchRef);
        var otherBranches = await SeedGuildWithBranchesAsync(otherGuildId, AnniversaryBranchRef);
        var definition = await AddDefinitionAsync(guildId, branches[0], "Survivor", "Section");

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildAttributionDefinitionsRepository>();

            (await repo.DeleteAsync(definition.Id, guildId, branches[1])).Should().BeFalse();
            (await repo.DeleteAsync(definition.Id, otherGuildId, otherBranches[0])).Should().BeFalse();
        }

        (await ReadAllAsync(guildId)).Should().ContainSingle(d => d.Id == definition.Id);
    }

    [Fact]
    public async Task ReorderAsync_OnlyRenumbersRowsOfTheGivenBranch()
    {
        const string guildId = "986000000000000112";
        var branches = await SeedGuildWithBranchesAsync(guildId, AnniversaryBranchRef, ForeverBranchRef);
        var a1 = await AddDefinitionAsync(guildId, branches[0], "A1", null);
        var a2 = await AddDefinitionAsync(guildId, branches[0], "A2", null);
        var a3 = await AddDefinitionAsync(guildId, branches[0], "A3", null);
        var b1 = await AddDefinitionAsync(guildId, branches[1], "B1", null);
        var b2 = await AddDefinitionAsync(guildId, branches[1], "B2", null);

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildAttributionDefinitionsRepository>();

            // b2's ID is smuggled into branch A's order: it must be ignored, not moved.
            await repo.ReorderAsync(guildId, branches[0], [a3.Id, b2.Id, a1.Id, a2.Id]);
        }

        var all = await ReadAllAsync(guildId);
        all.Single(d => d.Id == a3.Id).SortOrder.Should().Be(0);
        all.Single(d => d.Id == a1.Id).SortOrder.Should().Be(2);
        all.Single(d => d.Id == a2.Id).SortOrder.Should().Be(3);
        all.Single(d => d.Id == b1.Id).SortOrder.Should().Be(0);
        all.Single(d => d.Id == b2.Id).SortOrder.Should().Be(1);
    }

    [Fact]
    public async Task SetSectionIconAsync_IsIsolatedBetweenBranchesAndGuilds()
    {
        const string guildId = "986000000000000113";
        const string otherGuildId = "986000000000000114";
        var branches = await SeedGuildWithBranchesAsync(guildId, AnniversaryBranchRef, ForeverBranchRef);
        var otherBranches = await SeedGuildWithBranchesAsync(otherGuildId, AnniversaryBranchRef);
        await AddDefinitionAsync(guildId, branches[0], "A row", "Personals");
        await AddDefinitionAsync(guildId, branches[1], "B row", "Personals");
        await AddDefinitionAsync(otherGuildId, otherBranches[0], "Other row", "Personals");

        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGuildAttributionDefinitionsRepository>();

            var updated = await repo.SetSectionIconAsync(guildId, branches[0], null, "Personals", new SectionIconFields(AttributionIconSource.RaidMarker, null, RaidMarkerIcon.Star, null));

            updated.Should().Be(1);
        }

        var rows = await ReadAllAsync(guildId);
        rows.Single(d => d.GuildBranchId == branches[0]).SectionRaidMarker.Should().Be(RaidMarkerIcon.Star);
        rows.Single(d => d.GuildBranchId == branches[1]).SectionIconSource.Should().Be(AttributionIconSource.None);
        (await ReadAllAsync(otherGuildId)).Single().SectionIconSource.Should().Be(AttributionIconSource.None);
    }

    [Fact]
    public async Task DeletingTheGuild_CascadesToItsBranchScopedDefinitions()
    {
        const string guildId = "986000000000000115";
        var branches = await SeedGuildWithBranchesAsync(guildId, AnniversaryBranchRef, ForeverBranchRef);
        await AddDefinitionAsync(guildId, branches[0], "A", "S");
        await AddDefinitionAsync(guildId, branches[1], "B", "S");

        await SeedAsync(db => db.Guilds.Where(g => g.Id == guildId).ExecuteDeleteAsync());

        (await ReadAllAsync(guildId)).Should().BeEmpty();
    }

    [Fact]
    public async Task AddAsync_JoiningASectionWhenAnUngroupedSiblingExists_SkipsTheUngroupedRowWhenLookingForTheSection()
    {
        const string guildId = "986000000000000116";
        var branches = await SeedGuildWithBranchesAsync(guildId, AnniversaryBranchRef);
        await AddDefinitionAsync(guildId, branches[0], "Ungrouped", section: null);
        await AddDefinitionAsync(guildId, branches[0], "Personal 1", "Personals");

        var added = await AddDefinitionAsync(guildId, branches[0], "Personal 2", "Personals");

        added.SortOrder.Should().Be(2);
        (await ReadAllAsync(guildId)).OrderBy(d => d.SortOrder).Select(d => d.Label).Should().Equal("Ungrouped", "Personal 1", "Personal 2");
    }

    [Fact]
    public async Task GuildBranchNavigation_ResolvesTheOwningGuildBranch()
    {
        const string guildId = "986000000000000117";
        var branches = await SeedGuildWithBranchesAsync(guildId, ForeverBranchRef);
        var definition = await AddDefinitionAsync(guildId, branches[0], "Navigable", "S");
        var bossScoped = await AddDefinitionAsync(guildId, branches[0], "Boss row", "S", HydrossBossId);

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var loaded = await db.GuildAttributionDefinitions.AsNoTracking().Include(d => d.GuildBranch).SingleAsync(d => d.Id == definition.Id);

            loaded.GuildBranch.BranchId.Should().Be(ForeverBranchRef);
            loaded.GuildBranch.GuildId.Should().Be(guildId);
            loaded.RaidBoss.Should().BeNull();

            var bossRow = await db.GuildAttributionDefinitions.AsNoTracking().Include(d => d.RaidBoss).SingleAsync(d => d.Id == bossScoped.Id);
            bossRow.RaidBoss!.Id.Should().Be(HydrossBossId);
        }
    }
}
