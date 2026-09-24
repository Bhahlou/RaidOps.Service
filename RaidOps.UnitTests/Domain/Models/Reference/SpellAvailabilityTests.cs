using FluentAssertions;
using RaidOps.Domain.Models.Reference;

namespace RaidOps.UnitTests.Domain.Models.Reference;

/// <summary>
/// Unit tests for <see cref="SpellAvailability"/> — its <see cref="SpellAvailability.Spell"/> and
/// <see cref="SpellAvailability.Expansion"/> navigation properties exist for EF's fluent configuration
/// only and are never read by application code, so they need their own coverage (same as
/// <c>GuildBranchTests.Guild</c>).
/// </summary>
public class SpellAvailabilityTests
{
    [Fact]
    public void NavigationProperties_RoundTripTheirAssignedEntities()
    {
        var spell = new Spell { Id = 1022 };
        var expansion = new Expansion { Id = 5, Name = "Mists of Pandaria", ShortCode = "MoP" };

        var availability = new SpellAvailability
        {
            SpellId = spell.Id,
            ExpansionId = expansion.Id,
            NameEn = "Hand of Protection",
            NameFr = "Main de protection",
            NameDe = "Hand des Schutzes",
            IconUrl = "https://cdn/icon.jpg",
            Spell = spell,
            Expansion = expansion,
        };

        availability.Spell.Should().BeSameAs(spell);
        availability.Expansion.Should().BeSameAs(expansion);
        availability.NameEn.Should().Be("Hand of Protection");
    }

    [Fact]
    public void Spell_Availabilities_DefaultsToEmpty()
    {
        new Spell { Id = 1 }.Availabilities.Should().BeEmpty();
    }
}
