using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Attributions.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Attributions.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Raids;
using RaidOps.Domain.Models.Raids.Attributions;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Attributions.CommandHandlers;

/// <summary>
/// Unit tests for <see cref="SetRaidEventAttributionCommandHandler"/>.
/// </summary>
public class SetRaidEventAttributionCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IRaidEventRepository> _raidEvents = new();
    private readonly Mock<IGuildAttributionDefinitionsRepository> _definitions = new();
    private readonly Mock<IRaidEventAttributionsRepository> _attributions = new();
    private readonly Mock<IRaidBossRepository> _raidBosses = new();
    private readonly Mock<IAuditLogService> _auditLog = new();
    private readonly SetRaidEventAttributionCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const string RequesterId = "officer-1";
    private const int GuildBranchId = 7;
    private const int EventId = 42;
    private const int DefinitionId = 1;
    private const int CellId = 2;
    private const int CharacterId = 100;

    private static SetRaidEventAttributionCommand MakeCommand(int instanceIndex = 0, int? bossId = null) => new()
    {
        GuildId = GuildId, RequesterDiscordId = RequesterId, GuildBranchId = GuildBranchId, EventId = EventId,
        DefinitionId = DefinitionId, CellId = CellId, InstanceIndex = instanceIndex, CharacterId = CharacterId, BossId = bossId,
    };

    private static AttributionDefinitionCell MakeCell(AttributionCellKind kind = AttributionCellKind.NameSlot, List<int>? classIds = null, List<SpecRole>? roles = null, List<int>? specIds = null) => new()
    {
        Id = CellId,
        Kind = kind,
        RequiredClassIds = classIds ?? [],
        RequiredRoles = roles ?? [],
        RequiredSpecIds = specIds ?? [],
    };

    private static GuildAttributionDefinition MakeDefinition(bool isRepeatable = false, AttributionDefinitionCell? cell = null, int? raidBossId = null) => new()
    {
        Id = DefinitionId, GuildId = GuildId, IsRepeatable = isRepeatable, Cells = [cell ?? MakeCell()], RaidBossId = raidBossId,
    };

    private static RaidSlotAssignment MakeAssignment(int characterId = CharacterId, int classId = 1, SpecRole role = SpecRole.MeleeDps, int specId = 71) => new()
    {
        CharacterId = characterId,
        SpecId = specId,
        Character = new Character { Id = characterId, ClassId = classId },
        Spec = new Spec { Id = specId, Role = role },
    };

    public SetRaidEventAttributionCommandHandlerTests()
    {
        _sut = new SetRaidEventAttributionCommandHandler(_access.Object, _raidEvents.Object, _definitions.Object, _attributions.Object, _raidBosses.Object, _auditLog.Object);
    }

    private void SetupOfficer() => _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);

    [Fact]
    public async Task HandleAsync_NotOfficer_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_RaidEventNotFound_ReturnsRaidEventNotFound()
    {
        SetupOfficer();
        _raidEvents.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync((RaidEvent?)null);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidEventNotFound);
    }

    [Fact]
    public async Task HandleAsync_DefinitionNotFound_ReturnsAttributionDefinitionNotFound()
    {
        SetupOfficer();
        _raidEvents.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent { Id = EventId, Assignments = [] });
        _definitions.Setup(d => d.GetByIdAsync(DefinitionId, default)).ReturnsAsync((GuildAttributionDefinition?)null);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.AttributionDefinitionNotFound);
    }

    [Fact]
    public async Task HandleAsync_DefinitionBelongsToDifferentGuild_ReturnsAttributionDefinitionNotFound()
    {
        SetupOfficer();
        _raidEvents.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent { Id = EventId, Assignments = [] });
        _definitions.Setup(d => d.GetByIdAsync(DefinitionId, default)).ReturnsAsync(new GuildAttributionDefinition { Id = DefinitionId, GuildId = "other-guild", Cells = [MakeCell()] });

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.AttributionDefinitionNotFound);
    }

    [Fact]
    public async Task HandleAsync_CellNotFound_ReturnsAttributionCellNotFound()
    {
        SetupOfficer();
        _raidEvents.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent { Id = EventId, Assignments = [] });
        _definitions.Setup(d => d.GetByIdAsync(DefinitionId, default)).ReturnsAsync(new GuildAttributionDefinition { Id = DefinitionId, GuildId = GuildId, Cells = [] });

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.AttributionCellNotFound);
    }

    [Fact]
    public async Task HandleAsync_CellIsIcon_ReturnsAttributionCellNotNameSlot()
    {
        SetupOfficer();
        _raidEvents.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent { Id = EventId, Assignments = [] });
        _definitions.Setup(d => d.GetByIdAsync(DefinitionId, default)).ReturnsAsync(MakeDefinition(cell: MakeCell(AttributionCellKind.Icon)));

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.AttributionCellNotNameSlot);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(50)]
    public async Task HandleAsync_InstanceIndexOutOfBounds_ReturnsInvalidInstanceIndex(int instanceIndex)
    {
        SetupOfficer();
        _raidEvents.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent { Id = EventId, Assignments = [] });
        _definitions.Setup(d => d.GetByIdAsync(DefinitionId, default)).ReturnsAsync(MakeDefinition(isRepeatable: true));

        var result = await _sut.HandleAsync(MakeCommand(instanceIndex));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidInstanceIndex);
    }

    [Fact]
    public async Task HandleAsync_NonZeroInstanceIndexOnNonRepeatableRow_ReturnsInvalidInstanceIndex()
    {
        SetupOfficer();
        _raidEvents.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent { Id = EventId, Assignments = [] });
        _definitions.Setup(d => d.GetByIdAsync(DefinitionId, default)).ReturnsAsync(MakeDefinition(isRepeatable: false));

        var result = await _sut.HandleAsync(MakeCommand(instanceIndex: 1));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidInstanceIndex);
    }

    [Fact]
    public async Task HandleAsync_CharacterNotSeated_ReturnsCharacterNotSeatedInEvent()
    {
        SetupOfficer();
        _raidEvents.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent { Id = EventId, Assignments = [] });
        _definitions.Setup(d => d.GetByIdAsync(DefinitionId, default)).ReturnsAsync(MakeDefinition());

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.CharacterNotSeatedInEvent);
    }

    [Fact]
    public async Task HandleAsync_ClassRestrictionNotMet_ReturnsCharacterDoesNotMeetSlotRequirement()
    {
        SetupOfficer();
        _raidEvents.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent { Id = EventId, Assignments = [MakeAssignment(classId: 1)] });
        _definitions.Setup(d => d.GetByIdAsync(DefinitionId, default)).ReturnsAsync(MakeDefinition(cell: MakeCell(classIds: [9])));

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.CharacterDoesNotMeetSlotRequirement);
    }

    [Fact]
    public async Task HandleAsync_RoleRestrictionNotMet_ReturnsCharacterDoesNotMeetSlotRequirement()
    {
        SetupOfficer();
        _raidEvents.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent { Id = EventId, Assignments = [MakeAssignment(role: SpecRole.MeleeDps)] });
        _definitions.Setup(d => d.GetByIdAsync(DefinitionId, default)).ReturnsAsync(MakeDefinition(cell: MakeCell(roles: [SpecRole.Tank])));

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.CharacterDoesNotMeetSlotRequirement);
    }

    [Fact]
    public async Task HandleAsync_SpecRestrictionNotMet_ReturnsCharacterDoesNotMeetSlotRequirement()
    {
        SetupOfficer();
        _raidEvents.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent { Id = EventId, Assignments = [MakeAssignment(specId: 71)] });
        _definitions.Setup(d => d.GetByIdAsync(DefinitionId, default)).ReturnsAsync(MakeDefinition(cell: MakeCell(specIds: [253])));

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.CharacterDoesNotMeetSlotRequirement);
    }

    [Fact]
    public async Task HandleAsync_Success_FillsSlotAndLogs()
    {
        SetupOfficer();
        _raidEvents.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent { Id = EventId, Assignments = [MakeAssignment(classId: 9, role: SpecRole.RangedDps, specId: 265)] });
        _definitions.Setup(d => d.GetByIdAsync(DefinitionId, default)).ReturnsAsync(MakeDefinition(isRepeatable: true, cell: MakeCell(classIds: [9], roles: [SpecRole.RangedDps], specIds: [265])));

        var result = await _sut.HandleAsync(MakeCommand(instanceIndex: 3));

        result.IsSuccess.Should().BeTrue();
        _attributions.Verify(a => a.SetAsync(EventId, CellId, DefinitionId, 3, CharacterId, RequesterId, default), Times.Once);
        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.RaidEventAttributionUpdated,
            It.Is<Dictionary<string, string>>(v => v["definitionId"] == DefinitionId.ToString() && v["characterId"] == CharacterId.ToString()),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Success_NoRestrictions_AnyoneSeatedCanFill()
    {
        SetupOfficer();
        _raidEvents.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent { Id = EventId, Assignments = [MakeAssignment()] });
        _definitions.Setup(d => d.GetByIdAsync(DefinitionId, default)).ReturnsAsync(MakeDefinition());

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _attributions.Verify(a => a.SetAsync(EventId, CellId, DefinitionId, 0, CharacterId, RequesterId, default), Times.Once);
    }

    // ── BossId scope ─────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_CommandBossIdDoesNotMatchDefinitionScope_ReturnsDefinitionBossMismatch()
    {
        SetupOfficer();
        _raidEvents.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent { Id = EventId, Assignments = [MakeAssignment()] });
        _definitions.Setup(d => d.GetByIdAsync(DefinitionId, default)).ReturnsAsync(MakeDefinition(raidBossId: 14));

        var result = await _sut.HandleAsync(MakeCommand()); // BossId defaults to null — mismatches the row's RaidBossId=14

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.DefinitionBossMismatch);
        _attributions.Verify(a => a.SetAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_BossIdGiven_BossDoesNotExist_ReturnsBossNotTargetedByEvent()
    {
        SetupOfficer();
        _raidEvents.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent { Id = EventId, Assignments = [MakeAssignment()] });
        _definitions.Setup(d => d.GetByIdAsync(DefinitionId, default)).ReturnsAsync(MakeDefinition(raidBossId: 14));
        _raidBosses.Setup(b => b.GetByIdAsync(14, default)).ReturnsAsync((RaidBoss?)null);

        var result = await _sut.HandleAsync(MakeCommand(bossId: 14));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.BossNotTargetedByEvent);
    }

    [Fact]
    public async Task HandleAsync_BossIdGiven_BossZoneNotTargetedByEvent_ReturnsBossNotTargetedByEvent()
    {
        SetupOfficer();
        _raidEvents.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default))
            .ReturnsAsync(new RaidEvent { Id = EventId, Assignments = [MakeAssignment()], TargetZones = [new RaidEventZone { RaidZoneId = 1 }] });
        _definitions.Setup(d => d.GetByIdAsync(DefinitionId, default)).ReturnsAsync(MakeDefinition(raidBossId: 14));
        _raidBosses.Setup(b => b.GetByIdAsync(14, default)).ReturnsAsync(new RaidBoss { Id = 14, Name = "Hydross the Unstable", RaidZoneId = 4 });

        var result = await _sut.HandleAsync(MakeCommand(bossId: 14));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.BossNotTargetedByEvent);
    }

    [Fact]
    public async Task HandleAsync_BossIdGiven_BossZoneTargetedByEvent_Succeeds()
    {
        SetupOfficer();
        _raidEvents.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default))
            .ReturnsAsync(new RaidEvent { Id = EventId, Assignments = [MakeAssignment()], TargetZones = [new RaidEventZone { RaidZoneId = 4 }] });
        _definitions.Setup(d => d.GetByIdAsync(DefinitionId, default)).ReturnsAsync(MakeDefinition(raidBossId: 14));
        _raidBosses.Setup(b => b.GetByIdAsync(14, default)).ReturnsAsync(new RaidBoss { Id = 14, Name = "Hydross the Unstable", RaidZoneId = 4 });

        var result = await _sut.HandleAsync(MakeCommand(bossId: 14));

        result.IsSuccess.Should().BeTrue();
        _attributions.Verify(a => a.SetAsync(EventId, CellId, DefinitionId, 0, CharacterId, RequesterId, default), Times.Once);
    }
}
