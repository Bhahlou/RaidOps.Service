using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Attributions.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Attributions.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Attributions.CommandHandlers;

/// <summary>
/// Unit tests for <see cref="SetAttributionSectionIconCommandHandler"/>.
/// </summary>
public class SetAttributionSectionIconCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IGuildBranchesRepository> _branches = new();
    private readonly Mock<IGuildAttributionDefinitionsRepository> _definitions = new();
    private readonly Mock<ISpellRepository> _spells = new();
    private readonly Mock<IAuditLogService> _auditLog = new();
    private readonly SetAttributionSectionIconCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const string RequesterId = "officer-1";
    private const int BranchId = 5;

    private static readonly SetAttributionSectionIconCommand Command = new()
    {
        GuildId = GuildId,
        GuildBranchId = BranchId,
        RequesterDiscordId = RequesterId,
        Section = "Interrupts",
        IconSource = AttributionIconSource.RaidMarker,
        RaidMarker = RaidMarkerIcon.Skull,
    };

    public SetAttributionSectionIconCommandHandlerTests()
    {
        _branches.Setup(b => b.GetCurrentExpansionIdAsync(GuildId, BranchId, default)).ReturnsAsync(2);
        _sut = new SetAttributionSectionIconCommandHandler(_access.Object, _branches.Object, _definitions.Object, _spells.Object, _auditLog.Object);
    }

    private void SetupOfficer() => _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, BranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);

    [Fact]
    public async Task HandleAsync_NotOfficer_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, BranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
        _definitions.Verify(
            d => d.SetSectionIconAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<SectionIconFields>(), default),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SpellIconWithoutSpellId_ReturnsInvalidRequest()
    {
        SetupOfficer();
        var command = new SetAttributionSectionIconCommand { GuildId = GuildId, GuildBranchId = BranchId, RequesterDiscordId = RequesterId, Section = "Interrupts", IconSource = AttributionIconSource.Spell, SpellId = null };

        var result = await _sut.HandleAsync(command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidRequest);
    }

    [Fact]
    public async Task HandleAsync_SpellIconWithUnknownSpellId_ReturnsSpellNotFound()
    {
        SetupOfficer();
        _spells.Setup(s => s.GetAvailabilityAsync(999, 2, default)).ReturnsAsync((SpellAvailability?)null);
        var command = new SetAttributionSectionIconCommand { GuildId = GuildId, GuildBranchId = BranchId, RequesterDiscordId = RequesterId, Section = "Interrupts", IconSource = AttributionIconSource.Spell, SpellId = 999 };

        var result = await _sut.HandleAsync(command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.SpellNotFound);
    }

    [Fact]
    public async Task HandleAsync_NoRowUsesThisSection_ReturnsAttributionDefinitionNotFound()
    {
        SetupOfficer();
        _definitions.Setup(d => d.SetSectionIconAsync(GuildId, BranchId, null, "Interrupts", new SectionIconFields(AttributionIconSource.RaidMarker, null, RaidMarkerIcon.Skull, null), default)).ReturnsAsync(0);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.AttributionDefinitionNotFound);
        _auditLog.Verify(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GuildAuditAction>(), It.IsAny<Dictionary<string, string>>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Success_UpdatesAndLogs()
    {
        SetupOfficer();
        _definitions.Setup(d => d.SetSectionIconAsync(GuildId, BranchId, null, "Interrupts", new SectionIconFields(AttributionIconSource.RaidMarker, null, RaidMarkerIcon.Skull, null), default)).ReturnsAsync(3);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.AttributionTemplateUpdated,
            It.Is<Dictionary<string, string>>(v => v["section"] == "Interrupts"),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_BossScoped_PassesRaidBossIdThrough()
    {
        SetupOfficer();
        var command = new SetAttributionSectionIconCommand
        {
            GuildId = GuildId, GuildBranchId = BranchId, RequesterDiscordId = RequesterId, RaidBossId = 14, Section = "Interrupts",
            IconSource = AttributionIconSource.RaidMarker, RaidMarker = RaidMarkerIcon.Skull,
        };
        _definitions.Setup(d => d.SetSectionIconAsync(GuildId, BranchId, 14, "Interrupts", new SectionIconFields(AttributionIconSource.RaidMarker, null, RaidMarkerIcon.Skull, null), default)).ReturnsAsync(1);

        var result = await _sut.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        _definitions.Verify(d => d.SetSectionIconAsync(GuildId, BranchId, 14, "Interrupts", new SectionIconFields(AttributionIconSource.RaidMarker, null, RaidMarkerIcon.Skull, null), default), Times.Once);
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
        _definitions.Verify(
            d => d.SetSectionIconAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<SectionIconFields>(), default),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SpellAvailableOnTheBranchsExpansion_Succeeds()
    {
        SetupOfficer();
        var command = new SetAttributionSectionIconCommand { GuildId = GuildId, GuildBranchId = BranchId, RequesterDiscordId = RequesterId, Section = "Interrupts", IconSource = AttributionIconSource.Spell, SpellId = 2825 };
        _spells.Setup(s => s.GetAvailabilityAsync(2825, 2, default)).ReturnsAsync(new SpellAvailability { SpellId = 2825, ExpansionId = 2 });
        _definitions.Setup(d => d.SetSectionIconAsync(GuildId, BranchId, null, "Interrupts", new SectionIconFields(AttributionIconSource.Spell, 2825, null, null), default)).ReturnsAsync(1);

        var result = await _sut.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
    }
}
