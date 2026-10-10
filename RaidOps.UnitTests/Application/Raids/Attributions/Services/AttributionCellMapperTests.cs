using FluentAssertions;
using RaidOps.Application.Contracts.Raids.Attributions.Commands;
using RaidOps.Application.Implementations.Raids.Attributions.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids.Attributions;
using RaidOps.Domain.Models.Reference;

namespace RaidOps.UnitTests.Application.Raids.Attributions.Services;

/// <summary>
/// Unit tests for <see cref="AttributionCellMapper"/>.
/// </summary>
public class AttributionCellMapperTests
{
    // ── ToEntity ─────────────────────────────────────────────────────────────

    [Fact]
    public void ToEntity_IconCell_MapsIconFieldsAndBlanksNameSlotFields()
    {
        var request = new AttributionCellRequest
        {
            Kind = AttributionCellKind.Icon,
            IconSource = AttributionIconSource.RaidMarker,
            RaidMarker = RaidMarkerIcon.Skull,
            SlotLabel = "should be dropped",
            RequiredClassIds = [1, 2],
            RequiredRoles = [SpecRole.Tank],
            RequiredSpecIds = [71],
        };

        var entity = AttributionCellMapper.ToEntity(request, 3);

        entity.CellIndex.Should().Be(3);
        entity.Kind.Should().Be(AttributionCellKind.Icon);
        entity.IconSource.Should().Be(AttributionIconSource.RaidMarker);
        entity.RaidMarker.Should().Be(RaidMarkerIcon.Skull);
        entity.SlotLabel.Should().BeNull();
        entity.RequiredClassIds.Should().BeEmpty();
        entity.RequiredRoles.Should().BeEmpty();
        entity.RequiredSpecIds.Should().BeEmpty();
    }

    [Fact]
    public void ToEntity_IconCell_OnlyKeepsTheFieldMatchingItsOwnIconSource()
    {
        var request = new AttributionCellRequest
        {
            Kind = AttributionCellKind.Icon,
            IconSource = AttributionIconSource.Spell,
            SpellId = 29166,
            RaidMarker = RaidMarkerIcon.Skull,
            StaticRole = SpecRole.Tank,
        };

        var entity = AttributionCellMapper.ToEntity(request, 0);

        entity.SpellId.Should().Be(29166);
        entity.RaidMarker.Should().BeNull();
        entity.StaticRole.Should().BeNull();
    }

    [Fact]
    public void ToEntity_IconCellWithStaticRoleSource_MapsStaticRoleFieldAndBlanksTheOthers()
    {
        var request = new AttributionCellRequest
        {
            Kind = AttributionCellKind.Icon,
            IconSource = AttributionIconSource.StaticRole,
            StaticRole = SpecRole.Healer,
            SpellId = 29166,
            RaidMarker = RaidMarkerIcon.Skull,
        };

        var entity = AttributionCellMapper.ToEntity(request, 0);

        entity.StaticRole.Should().Be(SpecRole.Healer);
        entity.SpellId.Should().BeNull();
        entity.RaidMarker.Should().BeNull();
    }

    [Fact]
    public void ToEntity_NameSlotCell_MapsNameSlotFieldsAndForcesIconSourceToNone()
    {
        // A well-formed NameSlot request (mirroring what the dialog's toPayload() ever actually
        // sends) leaves IconSource at its None default — the front end never touches it for a
        // NameSlot cell.
        var request = new AttributionCellRequest
        {
            Kind = AttributionCellKind.NameSlot,
            SlotLabel = "Tank",
            RequiredClassIds = [1],
            RequiredRoles = [SpecRole.Tank],
            RequiredSpecIds = [73],
        };

        var entity = AttributionCellMapper.ToEntity(request, 1);

        entity.IconSource.Should().Be(AttributionIconSource.None);
        entity.RaidMarker.Should().BeNull();
        entity.SlotLabel.Should().Be("Tank");
        entity.RequiredClassIds.Should().Equal(1);
        entity.RequiredRoles.Should().Equal(SpecRole.Tank);
        entity.RequiredSpecIds.Should().Equal(73);
    }

    [Fact]
    public void ToEntity_NameSlotCellWithAnIconSourceSetAnyway_StillCopiesTheMatchingIconField()
    {
        // Unlike IconSource itself (forced to None off `Kind`), SpellId/RaidMarker/StaticRole are
        // each gated on the request's OWN IconSource, not on Kind — so a malformed request that
        // sets both Kind=NameSlot and IconSource=RaidMarker still comes through with a RaidMarker
        // value on the entity. The front end never actually sends this combination (see the test
        // above), but this pins the mapper's real behavior instead of an assumed one.
        var request = new AttributionCellRequest { Kind = AttributionCellKind.NameSlot, IconSource = AttributionIconSource.RaidMarker, RaidMarker = RaidMarkerIcon.Skull };

        var entity = AttributionCellMapper.ToEntity(request, 0);

        entity.IconSource.Should().Be(AttributionIconSource.None);
        entity.RaidMarker.Should().Be(RaidMarkerIcon.Skull);
    }

    // ── ToEntities ───────────────────────────────────────────────────────────

    [Fact]
    public void ToEntities_AssignsCellIndexFromPosition()
    {
        var requests = new[] { new AttributionCellRequest { Kind = AttributionCellKind.Icon }, new AttributionCellRequest { Kind = AttributionCellKind.NameSlot } };

        var entities = AttributionCellMapper.ToEntities(requests);

        entities.Select(e => e.CellIndex).Should().Equal(0, 1);
    }

    // ── ToResponse ───────────────────────────────────────────────────────────

    [Fact]
    public void ToResponse_MapsEveryFieldIncludingTheLinkedSpellIconUrl()
    {
        var cell = new AttributionDefinitionCell
        {
            Id = 42,
            Kind = AttributionCellKind.Icon,
            IconSource = AttributionIconSource.Spell,
            SpellId = 29166,
            Spell = new Spell { Id = 29166, Availabilities = [new SpellAvailability { SpellId = 29166, ExpansionId = 2, NameEn = "Innervate", NameFr = "x", NameDe = "y", IconUrl = "https://cdn/innervate.jpg" }] },
            SlotLabel = null,
            RequiredClassIds = [1],
            RequiredRoles = [SpecRole.Healer],
            RequiredSpecIds = [65],
        };

        var response = AttributionCellMapper.ToResponse(cell, 2);

        response.Id.Should().Be(42);
        response.Kind.Should().Be(AttributionCellKind.Icon);
        response.SpellId.Should().Be(29166);
        response.SpellIconUrl.Should().Be("https://cdn/innervate.jpg");
        response.RequiredClassIds.Should().Equal(1);
        response.RequiredRoles.Should().Equal(SpecRole.Healer);
        response.RequiredSpecIds.Should().Equal(65);
    }

    [Fact]
    public void ToResponse_NoLinkedSpell_SpellIconUrlIsNull()
    {
        var cell = new AttributionDefinitionCell { Id = 1, Kind = AttributionCellKind.NameSlot, Spell = null };

        var response = AttributionCellMapper.ToResponse(cell, 2);

        response.SpellIconUrl.Should().BeNull();
    }

    // ── Icon resolution per expansion ────────────────────────────────────────

    private static Spell SpellWith(params (int ExpansionId, string IconUrl)[] availabilities) => new()
    {
        Id = 2825,
        Availabilities = availabilities.Select(a => new SpellAvailability { SpellId = 2825, ExpansionId = a.ExpansionId, IconUrl = a.IconUrl }).ToList(),
    };

    [Fact]
    public void ToResponse_SpellAvailableOnSeveralExpansions_UsesTheRequestedExpansionsIcon()
    {
        var cell = new AttributionDefinitionCell { Kind = AttributionCellKind.Icon, IconSource = AttributionIconSource.Spell, SpellId = 2825, Spell = SpellWith((11, "https://cdn/retail.jpg"), (2, "https://cdn/tbc.jpg"), (12, "https://cdn/forever.jpg")) };

        AttributionCellMapper.ToResponse(cell, 2).SpellIconUrl.Should().Be("https://cdn/tbc.jpg");
        AttributionCellMapper.ToResponse(cell, 12).SpellIconUrl.Should().Be("https://cdn/forever.jpg");
    }

    [Fact]
    public void ToResponse_SpellNotObservedOnTheRequestedExpansion_FallsBackToAnotherExpansionsIcon()
    {
        var cell = new AttributionDefinitionCell { Kind = AttributionCellKind.Icon, IconSource = AttributionIconSource.Spell, SpellId = 2825, Spell = SpellWith((11, "https://cdn/retail.jpg")) };

        AttributionCellMapper.ToResponse(cell, 2).SpellIconUrl.Should().Be("https://cdn/retail.jpg");
    }

    [Fact]
    public void ToResponse_SpellWithNoAvailabilityAtAll_SpellIconUrlIsNull()
    {
        var cell = new AttributionDefinitionCell { Kind = AttributionCellKind.Icon, IconSource = AttributionIconSource.Spell, SpellId = 2825, Spell = SpellWith() };

        AttributionCellMapper.ToResponse(cell, 2).SpellIconUrl.Should().BeNull();
    }

    [Fact]
    public void ToDefinitionResponse_ResolvesSectionAndCellIconsOnTheGivenExpansion()
    {
        var definition = new GuildAttributionDefinition
        {
            Id = 3, GuildId = "g", Label = "Bloodlust", Section = "Cooldowns", IsRepeatable = true, RaidBossId = 14, SortOrder = 4,
            SectionIconSource = AttributionIconSource.Spell, SectionSpellId = 2825,
            SectionSpell = SpellWith((2, "https://cdn/section-tbc.jpg"), (12, "https://cdn/section-forever.jpg")),
            Cells =
            [
                new AttributionDefinitionCell { Id = 1, Kind = AttributionCellKind.Icon, IconSource = AttributionIconSource.Spell, SpellId = 2825, Spell = SpellWith((2, "https://cdn/cell-tbc.jpg"), (12, "https://cdn/cell-forever.jpg")) },
            ],
        };

        var response = AttributionCellMapper.ToDefinitionResponse(definition, 12);

        response.Id.Should().Be(3);
        response.Label.Should().Be("Bloodlust");
        response.Section.Should().Be("Cooldowns");
        response.IsRepeatable.Should().BeTrue();
        response.RaidBossId.Should().Be(14);
        response.SortOrder.Should().Be(4);
        response.SectionSpellId.Should().Be(2825);
        response.SectionSpellIconUrl.Should().Be("https://cdn/section-forever.jpg");
        response.Cells.Single().SpellIconUrl.Should().Be("https://cdn/cell-forever.jpg");
    }

    [Fact]
    public void ToDefinitionResponse_NoSectionSpell_SectionSpellIconUrlIsNull()
    {
        var definition = new GuildAttributionDefinition { Id = 3, GuildId = "g", Label = "x", SectionSpell = null, Cells = [] };

        AttributionCellMapper.ToDefinitionResponse(definition, 2).SectionSpellIconUrl.Should().BeNull();
    }
}
