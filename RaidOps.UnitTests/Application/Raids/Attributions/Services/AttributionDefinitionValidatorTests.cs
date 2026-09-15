using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Attributions.Commands;
using RaidOps.Application.Implementations.Raids.Attributions.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Attributions.Services;

/// <summary>
/// Unit tests for <see cref="AttributionDefinitionValidator"/>.
/// </summary>
public class AttributionDefinitionValidatorTests
{
    private readonly Mock<ISpellRepository> _spells = new();

    private static AttributionCellRequest IconCell(AttributionIconSource source, int? spellId = null, RaidMarkerIcon? marker = null, SpecRole? role = null) => new()
    {
        Kind = AttributionCellKind.Icon,
        IconSource = source,
        SpellId = spellId,
        RaidMarker = marker,
        StaticRole = role,
    };

    private static AttributionCellRequest NameSlotCell() => new() { Kind = AttributionCellKind.NameSlot };

    [Fact]
    public async Task ValidateAsync_NoCells_ReturnsNoCellsInDefinition()
    {
        var result = await AttributionDefinitionValidator.ValidateAsync([], _spells.Object, default);

        result.Should().Be(ResponseDetail.NoCellsInDefinition);
    }

    [Fact]
    public async Task ValidateAsync_NameSlotCell_ReturnsNull()
    {
        var result = await AttributionDefinitionValidator.ValidateAsync([NameSlotCell()], _spells.Object, default);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAsync_UnknownCellKind_ReturnsInvalidRequest()
    {
        var cell = new AttributionCellRequest { Kind = (AttributionCellKind)99 };

        var result = await AttributionDefinitionValidator.ValidateAsync([cell], _spells.Object, default);

        result.Should().Be(ResponseDetail.InvalidRequest);
    }

    // ── Icon: Spell ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateAsync_SpellIcon_NoSpellId_ReturnsInvalidRequest()
    {
        var result = await AttributionDefinitionValidator.ValidateAsync([IconCell(AttributionIconSource.Spell)], _spells.Object, default);

        result.Should().Be(ResponseDetail.InvalidRequest);
    }

    [Fact]
    public async Task ValidateAsync_SpellIcon_SpellNotFound_ReturnsSpellNotFound()
    {
        _spells.Setup(s => s.GetByIdAsync(29166, default)).ReturnsAsync((Spell?)null);

        var result = await AttributionDefinitionValidator.ValidateAsync([IconCell(AttributionIconSource.Spell, spellId: 29166)], _spells.Object, default);

        result.Should().Be(ResponseDetail.SpellNotFound);
    }

    [Fact]
    public async Task ValidateAsync_SpellIcon_SpellFound_ReturnsNull()
    {
        _spells.Setup(s => s.GetByIdAsync(29166, default)).ReturnsAsync(new Spell { Id = 29166, NameEn = "Innervate", NameFr = "Vigueur naturelle", NameDe = "Winterschlaf", IconUrl = "https://cdn/innervate.jpg" });

        var result = await AttributionDefinitionValidator.ValidateAsync([IconCell(AttributionIconSource.Spell, spellId: 29166)], _spells.Object, default);

        result.Should().BeNull();
    }

    // ── Icon: RaidMarker ─────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateAsync_RaidMarkerIcon_NoMarker_ReturnsInvalidRequest()
    {
        var result = await AttributionDefinitionValidator.ValidateAsync([IconCell(AttributionIconSource.RaidMarker)], _spells.Object, default);

        result.Should().Be(ResponseDetail.InvalidRequest);
    }

    [Fact]
    public async Task ValidateAsync_RaidMarkerIcon_MarkerSet_ReturnsNull()
    {
        var result = await AttributionDefinitionValidator.ValidateAsync([IconCell(AttributionIconSource.RaidMarker, marker: RaidMarkerIcon.Skull)], _spells.Object, default);

        result.Should().BeNull();
    }

    // ── Icon: StaticRole ─────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateAsync_StaticRoleIcon_NoRole_ReturnsInvalidRequest()
    {
        var result = await AttributionDefinitionValidator.ValidateAsync([IconCell(AttributionIconSource.StaticRole)], _spells.Object, default);

        result.Should().Be(ResponseDetail.InvalidRequest);
    }

    [Fact]
    public async Task ValidateAsync_StaticRoleIcon_RoleSet_ReturnsNull()
    {
        var result = await AttributionDefinitionValidator.ValidateAsync([IconCell(AttributionIconSource.StaticRole, role: SpecRole.Tank)], _spells.Object, default);

        result.Should().BeNull();
    }

    // ── Icon: None ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateAsync_NoneIcon_ReturnsInvalidRequest()
    {
        var result = await AttributionDefinitionValidator.ValidateAsync([IconCell(AttributionIconSource.None)], _spells.Object, default);

        result.Should().Be(ResponseDetail.InvalidRequest);
    }

    // ── First failure wins ───────────────────────────────────────────────────

    [Fact]
    public async Task ValidateAsync_MultipleCells_ReturnsFirstFailureFound()
    {
        var cells = new List<AttributionCellRequest> { NameSlotCell(), IconCell(AttributionIconSource.None), IconCell(AttributionIconSource.RaidMarker) };

        var result = await AttributionDefinitionValidator.ValidateAsync(cells, _spells.Object, default);

        result.Should().Be(ResponseDetail.InvalidRequest);
    }
}
