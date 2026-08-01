using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Assignments.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Assignments.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Assignments.CommandHandlers;

public class AssignCharacterToSlotCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IRaidEventRepository> _raidEventRepository = new();
    private readonly Mock<ICharacterRepository> _characterRepository = new();
    private readonly Mock<IGuildMembershipRepository> _guildMembershipRepository = new();
    private readonly Mock<IRaidAvailabilityService> _raidAvailabilityService = new();
    private readonly Mock<IRaidLockoutConflictChecker> _raidLockoutConflictChecker = new();
    private readonly Mock<IRaidCompositionRepository> _raidCompositionRepository = new();
    private readonly AssignCharacterToSlotCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "officer-1";
    private const int EventId = 5;
    private const int CharacterId = 42;
    private const string PlayerDiscordId = "player-1";

    private static readonly DateTime EventStartsAtUtc = new(2026, 2, 4, 20, 0, 0, DateTimeKind.Utc);

    public AssignCharacterToSlotCommandHandlerTests()
    {
        _sut = new AssignCharacterToSlotCommandHandler(
            _access.Object, _raidEventRepository.Object, _characterRepository.Object, _guildMembershipRepository.Object,
            _raidAvailabilityService.Object, _raidLockoutConflictChecker.Object, _raidCompositionRepository.Object);

        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(MakeEvent());
        _characterRepository.Setup(r => r.GetByIdAsync(CharacterId, default)).ReturnsAsync(MakeCharacter());
        _guildMembershipRepository.Setup(r => r.GetByCharacterIdAsync(CharacterId, default))
            .ReturnsAsync([new GuildMembership { CharacterId = CharacterId, GuildId = GuildId, GuildBranchId = GuildBranchId }]);
        _raidAvailabilityService.Setup(s => s.IsPlayerUnavailableAsync(PlayerDiscordId, GuildId, GuildBranchId, It.IsAny<DateTime>(), default)).ReturnsAsync(false);
        _raidLockoutConflictChecker.Setup(c => c.FindConflictingZoneNameAsync(It.IsAny<RaidEvent>(), CharacterId, GuildId, GuildBranchId, default)).ReturnsAsync((string?)null);
        _characterRepository.Setup(r => r.GetRaidSpecsAsync(CharacterId, default))
            .ReturnsAsync([new CharacterRaidSpec { CharacterId = CharacterId, SpecId = 1, IsMain = true }]);
    }

    private static AssignCharacterToSlotCommand MakeCommand(int groupNumber = 1, int slotNumber = 1, int characterId = CharacterId) => new()
    {
        GuildId = GuildId,
        RequesterDiscordId = RequesterId,
        GuildBranchId = GuildBranchId,
        EventId = EventId,
        GroupNumber = groupNumber,
        SlotNumber = slotNumber,
        CharacterId = characterId,
    };

    private static RaidEvent MakeEvent(int groupCount = 2, int slotsPerGroup = 5, List<RaidSlotAssignment>? assignments = null, List<RaidEventZone>? targetZones = null) => new()
    {
        Id = EventId,
        GroupCount = groupCount,
        SlotsPerGroup = slotsPerGroup,
        StartsAtUtc = EventStartsAtUtc,
        Assignments = assignments ?? [],
        TargetZones = targetZones ?? [],
    };

    private static Character MakeCharacter(bool isActive = true) => new()
    {
        Id = CharacterId,
        UserDiscordId = PlayerDiscordId,
        IsActiveInRaidOps = isActive,
    };

    [Fact]
    public async Task HandleAsync_NotOfficer_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_EventNotFound_ReturnsRaidEventNotFound()
    {
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync((RaidEvent?)null);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidEventNotFound);
    }

    [Fact]
    public async Task HandleAsync_CharacterNotFound_ReturnsCharacterNotOnRoster()
    {
        _characterRepository.Setup(r => r.GetByIdAsync(CharacterId, default)).ReturnsAsync((Character?)null);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.CharacterNotOnRoster);
    }

    [Fact]
    public async Task HandleAsync_CharacterInactive_ReturnsCharacterNotOnRoster()
    {
        _characterRepository.Setup(r => r.GetByIdAsync(CharacterId, default)).ReturnsAsync(MakeCharacter(isActive: false));

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.CharacterNotOnRoster);
    }

    [Fact]
    public async Task HandleAsync_NoMembershipOnThisGuildBranch_ReturnsCharacterNotOnRoster()
    {
        _guildMembershipRepository.Setup(r => r.GetByCharacterIdAsync(CharacterId, default))
            .ReturnsAsync([new GuildMembership { CharacterId = CharacterId, GuildId = GuildId, GuildBranchId = GuildBranchId + 1 }]);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.CharacterNotOnRoster);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(3, 1)]
    [InlineData(1, 0)]
    [InlineData(1, 6)]
    public async Task HandleAsync_OutOfGridBounds_ReturnsInvalidGroupOrSlotNumber(int groupNumber, int slotNumber)
    {
        var result = await _sut.HandleAsync(MakeCommand(groupNumber, slotNumber));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidGroupOrSlotNumber);
    }

    [Fact]
    public async Task HandleAsync_SlotOccupiedByDifferentCharacter_ReturnsSlotOccupied()
    {
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(MakeEvent(
            assignments: [new RaidSlotAssignment { GroupNumber = 1, SlotNumber = 1, CharacterId = CharacterId + 1, AssignedPlayerDiscordId = "other-player" }]));

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.SlotOccupied);
    }

    [Fact]
    public async Task HandleAsync_SamePlayerAlreadyAssignedAnotherCharacterInEvent_ReturnsPlayerAlreadyAssignedInEvent()
    {
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(MakeEvent(
            assignments: [new RaidSlotAssignment { GroupNumber = 2, SlotNumber = 1, CharacterId = CharacterId + 1, AssignedPlayerDiscordId = PlayerDiscordId }]));

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.PlayerAlreadyAssignedInEvent);
    }

    [Fact]
    public async Task HandleAsync_PlayerDeclaredUnavailable_ReturnsMemberDeclaredAbsent()
    {
        _raidAvailabilityService.Setup(s => s.IsPlayerUnavailableAsync(PlayerDiscordId, GuildId, GuildBranchId, EventStartsAtUtc, default)).ReturnsAsync(true);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.MemberDeclaredAbsent);
        _raidCompositionRepository.Verify(r => r.AssignCharacterAsync(It.IsAny<RaidSlotAssignment>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_LockoutConflictDetected_ReturnsRaidLockoutConflictWithZoneName()
    {
        _raidLockoutConflictChecker.Setup(c => c.FindConflictingZoneNameAsync(It.IsAny<RaidEvent>(), CharacterId, GuildId, GuildBranchId, default))
            .ReturnsAsync("Zul'Gurub");

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidLockoutConflict);
        result.Detail.Should().Contain("Zul'Gurub");
        _raidCompositionRepository.Verify(r => r.AssignCharacterAsync(It.IsAny<RaidSlotAssignment>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_NoLockoutConflictNoAbsence_NewAssignmentDefaultsToMainSpec()
    {
        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _raidCompositionRepository.Verify(r => r.AssignCharacterAsync(It.Is<RaidSlotAssignment>(a =>
            a.RaidEventId == EventId && a.GroupNumber == 1 && a.SlotNumber == 1 &&
            a.CharacterId == CharacterId && a.SpecId == 1 &&
            a.AssignedPlayerDiscordId == PlayerDiscordId && a.AssignedByDiscordId == RequesterId),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_CharacterHasNoMainRaidSpec_ReturnsCharacterHasNoRaidSpec()
    {
        _characterRepository.Setup(r => r.GetRaidSpecsAsync(CharacterId, default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.CharacterHasNoRaidSpec);
        _raidCompositionRepository.Verify(r => r.AssignCharacterAsync(It.IsAny<RaidSlotAssignment>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_RepositioningWithinSameEvent_KeepsExistingSpecInsteadOfMainSpec()
    {
        // Character already occupies (1,1) with spec 77; dragged to the empty slot (1,2).
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(MakeEvent(
            assignments: [new RaidSlotAssignment { GroupNumber = 1, SlotNumber = 1, CharacterId = CharacterId, SpecId = 77, AssignedPlayerDiscordId = PlayerDiscordId }]));

        var result = await _sut.HandleAsync(MakeCommand(groupNumber: 1, slotNumber: 2));

        result.IsSuccess.Should().BeTrue();
        _raidCompositionRepository.Verify(r => r.AssignCharacterAsync(It.Is<RaidSlotAssignment>(a => a.SpecId == 77), default), Times.Once);
        _characterRepository.Verify(r => r.GetRaidSpecsAsync(CharacterId, default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_RedropOntoOwnCurrentSlot_Succeeds()
    {
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(MakeEvent(
            assignments: [new RaidSlotAssignment { GroupNumber = 1, SlotNumber = 1, CharacterId = CharacterId, SpecId = 77, AssignedPlayerDiscordId = PlayerDiscordId }]));

        var result = await _sut.HandleAsync(MakeCommand(groupNumber: 1, slotNumber: 1));

        result.IsSuccess.Should().BeTrue();
    }
}
