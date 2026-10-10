using RaidOps.Domain.Models.Reference;
using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Attributions.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Attributions.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids.Attributions;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Attributions.CommandHandlers;

/// <summary>
/// Unit tests for <see cref="UpdateGuildAttributionDefinitionCommandHandler"/>.
/// </summary>
public class UpdateGuildAttributionDefinitionCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IGuildBranchesRepository> _branches = new();
    private readonly Mock<IGuildAttributionDefinitionsRepository> _definitions = new();
    private readonly Mock<ISpellRepository> _spells = new();
    private readonly Mock<IAuditLogService> _auditLog = new();
    private readonly UpdateGuildAttributionDefinitionCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const string RequesterId = "officer-1";
    private const int DefinitionId = 7;
    private const int BranchId = 5;

    private static readonly UpdateGuildAttributionDefinitionCommand Command = new()
    {
        GuildId = GuildId,
        GuildBranchId = BranchId,
        RequesterDiscordId = RequesterId,
        DefinitionId = DefinitionId,
        Label = "Innervate",
        Section = "Personals",
        IsRepeatable = true,
        Cells = [new AttributionCellRequest { Kind = AttributionCellKind.NameSlot }],
    };

    public UpdateGuildAttributionDefinitionCommandHandlerTests()
    {
        _branches.Setup(b => b.GetCurrentExpansionIdAsync(GuildId, BranchId, default)).ReturnsAsync(2);
        _sut = new UpdateGuildAttributionDefinitionCommandHandler(_access.Object, _branches.Object, _definitions.Object, _spells.Object, _auditLog.Object);
    }

    private void SetupOfficer() => _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, BranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);

    [Fact]
    public async Task HandleAsync_NotOfficer_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, BranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
        _definitions.Verify(d => d.UpdateAsync(It.IsAny<GuildAttributionDefinition>(), GuildId, BranchId, default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_InvalidCells_ReturnsValidationError()
    {
        SetupOfficer();
        var invalidCommand = new UpdateGuildAttributionDefinitionCommand { GuildId = GuildId, GuildBranchId = BranchId, RequesterDiscordId = RequesterId, DefinitionId = DefinitionId, Label = "x", Cells = [] };

        var result = await _sut.HandleAsync(invalidCommand);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.NoCellsInDefinition);
    }

    [Fact]
    public async Task HandleAsync_DefinitionNotFound_ReturnsAttributionDefinitionNotFound()
    {
        SetupOfficer();
        _definitions.Setup(d => d.UpdateAsync(It.IsAny<GuildAttributionDefinition>(), GuildId, BranchId, default)).ReturnsAsync(false);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.AttributionDefinitionNotFound);
        _auditLog.Verify(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GuildAuditAction>(), It.IsAny<Dictionary<string, string>>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Success_UpdatesAndLogs()
    {
        SetupOfficer();
        GuildAttributionDefinition? updated = null;
        _definitions.Setup(d => d.UpdateAsync(It.IsAny<GuildAttributionDefinition>(), GuildId, BranchId, default))
            .Callback<GuildAttributionDefinition, string, int, CancellationToken>((d, _, _, _) => updated = d)
            .ReturnsAsync(true);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        updated.Should().NotBeNull();
        updated!.Id.Should().Be(DefinitionId);
        updated.Label.Should().Be("Innervate");
        updated.Cells.Should().HaveCount(1);

        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.AttributionTemplateUpdated,
            It.Is<Dictionary<string, string>>(v => v["label"] == "Innervate"),
            default), Times.Once);
    }

    // ── Guild-branch scoping ─────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_OfficerOfAnotherBranchOnly_ReturnsForbiddenUsingTheBranchAwareOverload()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, 99, default)).ReturnsAsync(GuildAccessLevel.Officer);

        var result = await _sut.HandleAsync(Command);

        result.Error.Should().Be(ResponseDetail.Forbidden);
        _access.Verify(a => a.GetAccessLevelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_GuildBranchNotFound_ReturnsGuildBranchNotFound()
    {
        SetupOfficer();
        _branches.Setup(b => b.GetCurrentExpansionIdAsync(GuildId, BranchId, default)).ReturnsAsync((int?)null);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildBranchNotFound);
        _definitions.Verify(d => d.UpdateAsync(It.IsAny<GuildAttributionDefinition>(), It.IsAny<string>(), It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Success_ScopesTheUpdateToTheCommandsGuildBranch()
    {
        SetupOfficer();
        GuildAttributionDefinition? updated = null;
        _definitions.Setup(d => d.UpdateAsync(It.IsAny<GuildAttributionDefinition>(), GuildId, BranchId, default))
            .Callback<GuildAttributionDefinition, string, int, CancellationToken>((d, _, _, _) => updated = d)
            .ReturnsAsync(true);

        await _sut.HandleAsync(Command);

        updated!.GuildBranchId.Should().Be(BranchId);
        updated.GuildId.Should().Be(GuildId);
    }

    [Fact]
    public async Task HandleAsync_SpellNotAvailableOnTheBranchsExpansion_ReturnsSpellNotFound()
    {
        SetupOfficer();
        var command = new UpdateGuildAttributionDefinitionCommand
        {
            GuildId = GuildId, GuildBranchId = BranchId, RequesterDiscordId = RequesterId, DefinitionId = DefinitionId, Label = "Bloodlust",
            Cells = [new AttributionCellRequest { Kind = AttributionCellKind.Icon, IconSource = AttributionIconSource.Spell, SpellId = 2825 }],
        };
        _spells.Setup(s => s.GetAvailabilityAsync(2825, 2, default)).ReturnsAsync((SpellAvailability?)null);

        var result = await _sut.HandleAsync(command);

        result.Error.Should().Be(ResponseDetail.SpellNotFound);
        _spells.Verify(s => s.GetAvailabilityAsync(2825, 2, default), Times.Once);
        _definitions.Verify(d => d.UpdateAsync(It.IsAny<GuildAttributionDefinition>(), It.IsAny<string>(), It.IsAny<int>(), default), Times.Never);
    }
}
