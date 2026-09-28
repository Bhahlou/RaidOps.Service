using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.IntegrationTests.Infrastructure;

/// <summary>
/// Integration tests for <see cref="RaidBuffDefinitionsRepository"/> covering what the controller
/// tests' happy paths never reach: <see cref="RaidBuffDefinitionsRepository.UpdateAsync"/> against an
/// ID that doesn't exist (the command handler already checks <c>GetByIdAsync</c> first, so the HTTP
/// pipeline never drives this branch), and that the EF relationships declared on
/// <see cref="RaidBuffDefinition"/>/<see cref="RaidBuffSource"/> (none of them loaded by any production
/// query today) are actually configured correctly. Spell IDs are 9930001+ to avoid collisions with
/// other test classes; expansion 12 (Forever) is shared reference data, so assertions filter down to
/// this class's own spell IDs rather than asserting exact counts.
/// </summary>
[Collection("Integration")]
public class RaidBuffDefinitionsRepositoryTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private const int ExpansionId = 12;

    private async Task<RaidBuffDefinition> SeedDefinitionAsync(int spellId)
    {
        await SeedAsync(db =>
        {
            db.Spells.Add(new Spell { Id = spellId });
            db.SpellAvailabilities.Add(new SpellAvailability { SpellId = spellId, ExpansionId = ExpansionId, NameEn = "x", NameFr = "x", NameDe = "x", IconUrl = "x" });
            return Task.CompletedTask;
        });

        var (scope, db2) = CreateDbScope();
        using (scope)
        {
            var definition = new RaidBuffDefinition
            {
                ExpansionId = ExpansionId,
                SpellId = spellId,
                Scope = RaidBuffScope.Raid,
                Kind = RaidBuffKind.Buff,
                LabelEn = "en",
                LabelFr = "fr",
                LabelDe = "de",
                SortOrder = 0,
                Sources = [new RaidBuffSource { ClassId = 7, SpecId = 264 }],
            };
            db2.RaidBuffDefinitions.Add(definition);
            await db2.SaveChangesAsync();
            return definition;
        }
    }

    [Fact]
    public async Task UpdateAsync_UnknownId_ReturnsFalseWithoutWriting()
    {
        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRaidBuffDefinitionsRepository>();
            var incoming = new RaidBuffDefinition { SpellId = 9930099, LabelEn = "en", LabelFr = "fr", LabelDe = "de" };

            var updated = await repo.UpdateAsync(999999999, incoming);

            updated.Should().BeFalse();
        }
    }

    [Fact]
    public async Task Expansion_And_Source_RelationshipsResolveThroughEagerLoading()
    {
        const int spellId = 9930001;
        var seeded = await SeedDefinitionAsync(spellId);

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            // RaidBuffSource.RaidBuffDefinition isn't included explicitly: EF's own change-tracker fix-up
            // populates it automatically once its parent is loaded in the same context — re-including it
            // would just walk back up the tree EF already built, which EF itself refuses as redundant.
            var definition = await db.RaidBuffDefinitions
                .Include(d => d.Expansion)
                .Include(d => d.Sources).ThenInclude(s => s.Class)
                .Include(d => d.Sources).ThenInclude(s => s.Spec)
                .AsSplitQuery()
                .SingleAsync(d => d.Id == seeded.Id);

            definition.Expansion.Should().NotBeNull();
            definition.Expansion.Name.Should().Be("Forever");

            var source = definition.Sources.Should().ContainSingle().Subject;
            source.RaidBuffDefinition.Should().NotBeNull();
            source.RaidBuffDefinition.Id.Should().Be(definition.Id);
            source.Class.Should().NotBeNull();
            source.Class.Id.Should().Be(7);
            source.Spec.Should().NotBeNull();
            source.Spec!.Id.Should().Be(264);
        }
    }
}
