using RaidOps.Domain.Models.Reference;
using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Attributions.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Attributions.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids;
using RaidOps.Domain.Models.Raids.Attributions;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Attributions.CommandHandlers;

/// <summary>
/// Unit tests for <see cref="CreateGuildAttributionDefinitionCommandHandler"/>.
/// </summary>
public class CreateGuildAttributionDefinitionCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IGuildBranchesRepository> _branches = new();
    private readonly Mock<IGuildAttributionDefinitionsRepository> _definitions = new();
    private readonly Mock<ISpellRepository> _spells = new();
    private readonly Mock<IRaidBossRepository> _raidBosses = new();
    private readonly Mock<IAuditLogService> _auditLog = new();
    private readonly CreateGuildAttributionDefinitionCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const string RequesterId = "officer-1";
    private const int BranchId = 5;

    private static readonly CreateGuildAttributionDefinitionCommand Command = new()
    {
        GuildId = GuildId,
        GuildBranchId = BranchId,
        RequesterDiscordId = RequesterId,
        Label = "Innervate",
        Section = "Personals",
        IsRepeatable = true,
        Cells = [new AttributionCellRequest { Kind = AttributionCellKind.NameSlot }],
    };

    public CreateGuildAttributionDefinitionCommandHandlerTests()
    {
        _branches.Setup(b => b.GetCurrentExpansionIdAsync(GuildId, BranchId, default)).ReturnsAsync(2);
        _sut = new CreateGuildAttributionDefinitionCommandHandler(_access.Object, _branches.Object, _definitions.Object, _spells.Object, _raidBosses.Object, _auditLog.Object);
    }

    private void SetupOfficer() => _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, BranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);

    [Fact]
    public async Task HandleAsync_NotOfficer_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, BranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
        _definitions.Verify(d => d.AddAsync(It.IsAny<GuildAttributionDefinition>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_InvalidCells_ReturnsValidationError()
    {
        SetupOfficer();
        var invalidCommand = new CreateGuildAttributionDefinitionCommand { GuildId = GuildId, GuildBranchId = BranchId, RequesterDiscordId = RequesterId, Label = "x", Cells = [] };

        var result = await _sut.HandleAsync(invalidCommand);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.NoCellsInDefinition);
        _definitions.Verify(d => d.AddAsync(It.IsAny<GuildAttributionDefinition>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Success_AddsDefinitionAndLogs()
    {
        SetupOfficer();
        GuildAttributionDefinition? added = null;
        _definitions.Setup(d => d.AddAsync(It.IsAny<GuildAttributionDefinition>(), default))
            .Callback<GuildAttributionDefinition, CancellationToken>((d, _) => added = d)
            .ReturnsAsync((GuildAttributionDefinition d, CancellationToken _) => d);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        added.Should().NotBeNull();
        added!.GuildId.Should().Be(GuildId);
        added.Label.Should().Be("Innervate");
        added.Section.Should().Be("Personals");
        added.IsRepeatable.Should().BeTrue();
        added.RaidBossId.Should().BeNull();
        added.CreatedByDiscordId.Should().Be(RequesterId);
        added.Cells.Should().HaveCount(1);

        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.AttributionTemplateUpdated,
            It.Is<Dictionary<string, string>>(v => v["label"] == "Innervate"),
            default), Times.Once);
    }

    // ── RaidBossId scope ─────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_RaidBossIdGiven_BossNotFound_ReturnsRaidBossNotFound()
    {
        SetupOfficer();
        _raidBosses.Setup(b => b.GetByIdAsync(999, default)).ReturnsAsync((RaidBoss?)null);
        var command = new CreateGuildAttributionDefinitionCommand
        {
            GuildId = GuildId, GuildBranchId = BranchId, RequesterDiscordId = RequesterId, Label = "Interrupt", RaidBossId = 999,
            Cells = [new AttributionCellRequest { Kind = AttributionCellKind.NameSlot }],
        };

        var result = await _sut.HandleAsync(command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidBossNotFound);
        _definitions.Verify(d => d.AddAsync(It.IsAny<GuildAttributionDefinition>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_RaidBossIdGiven_BossFound_ScopesTheNewRowToIt()
    {
        SetupOfficer();
        _raidBosses.Setup(b => b.GetByIdAsync(14, default)).ReturnsAsync(new RaidBoss { Id = 14, Name = "Hydross the Unstable", RaidZoneId = 4 });
        GuildAttributionDefinition? added = null;
        _definitions.Setup(d => d.AddAsync(It.IsAny<GuildAttributionDefinition>(), default))
            .Callback<GuildAttributionDefinition, CancellationToken>((d, _) => added = d)
            .ReturnsAsync((GuildAttributionDefinition d, CancellationToken _) => d);
        var command = new CreateGuildAttributionDefinitionCommand
        {
            GuildId = GuildId, GuildBranchId = BranchId, RequesterDiscordId = RequesterId, Label = "Interrupt", RaidBossId = 14,
            Cells = [new AttributionCellRequest { Kind = AttributionCellKind.NameSlot }],
        };

        var result = await _sut.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        added!.RaidBossId.Should().Be(14);
    }

    // ── Guild-branch scoping ─────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_OfficerOfAnotherBranchOnly_ReturnsForbiddenUsingTheBranchAwareOverload()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, 99, default)).ReturnsAsync(GuildAccessLevel.Officer);

        var result = await _sut.HandleAsync(Command);

        result.Error.Should().Be(ResponseDetail.Forbidden);
        _access.Verify(a => a.GetAccessLevelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _definitions.Verify(d => d.AddAsync(It.IsAny<GuildAttributionDefinition>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_GuildBranchNotFound_ReturnsGuildBranchNotFound()
    {
        SetupOfficer();
        _branches.Setup(b => b.GetCurrentExpansionIdAsync(GuildId, BranchId, default)).ReturnsAsync((int?)null);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildBranchNotFound);
        _definitions.Verify(d => d.AddAsync(It.IsAny<GuildAttributionDefinition>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Success_CreatesTheRowOnTheCommandsGuildBranch()
    {
        SetupOfficer();
        GuildAttributionDefinition? added = null;
        _definitions.Setup(d => d.AddAsync(It.IsAny<GuildAttributionDefinition>(), default))
            .Callback<GuildAttributionDefinition, CancellationToken>((d, _) => added = d)
            .ReturnsAsync((GuildAttributionDefinition d, CancellationToken _) => d);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        added!.GuildBranchId.Should().Be(BranchId);
    }

    [Fact]
    public async Task HandleAsync_SpellNotAvailableOnTheBranchsExpansion_ReturnsSpellNotFound()
    {
        SetupOfficer();
        var command = new CreateGuildAttributionDefinitionCommand
        {
            GuildId = GuildId, GuildBranchId = BranchId, RequesterDiscordId = RequesterId, Label = "Bloodlust",
            Cells = [new AttributionCellRequest { Kind = AttributionCellKind.Icon, IconSource = AttributionIconSource.Spell, SpellId = 2825 }],
        };
        _spells.Setup(s => s.GetAvailabilityAsync(2825, 2, default)).ReturnsAsync((SpellAvailability?)null);

        var result = await _sut.HandleAsync(command);

        result.Error.Should().Be(ResponseDetail.SpellNotFound);
        _spells.Verify(s => s.GetAvailabilityAsync(2825, 2, default), Times.Once);
        _definitions.Verify(d => d.AddAsync(It.IsAny<GuildAttributionDefinition>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SpellAvailableOnTheBranchsExpansion_Succeeds()
    {
        SetupOfficer();
        var command = new CreateGuildAttributionDefinitionCommand
        {
            GuildId = GuildId, GuildBranchId = BranchId, RequesterDiscordId = RequesterId, Label = "Bloodlust",
            Cells = [new AttributionCellRequest { Kind = AttributionCellKind.Icon, IconSource = AttributionIconSource.Spell, SpellId = 2825 }],
        };
        _spells.Setup(s => s.GetAvailabilityAsync(2825, 2, default)).ReturnsAsync(new SpellAvailability { SpellId = 2825, ExpansionId = 2 });
        _definitions.Setup(d => d.AddAsync(It.IsAny<GuildAttributionDefinition>(), default))
            .ReturnsAsync((GuildAttributionDefinition d, CancellationToken _) => d);

        var result = await _sut.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
    }
}
