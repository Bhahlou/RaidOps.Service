using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Assignments.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Assignments.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Raids;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Assignments.CommandHandlers;

public class AssignCharacterToSlotCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IRaidEventRepository> _raidEventRepository = new();
    private readonly Mock<ICharacterRepository> _characterRepository = new();
    private readonly Mock<IRaidSlotEligibilityValidator> _raidSlotEligibilityValidator = new();
    private readonly Mock<IRaidCompositionRepository> _raidCompositionRepository = new();
    private readonly Mock<IRaidCompositionNotifier> _raidCompositionNotifier = new();
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
            _access.Object, _raidEventRepository.Object, _characterRepository.Object, _raidSlotEligibilityValidator.Object,
            _raidCompositionRepository.Object, _raidCompositionNotifier.Object);

        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(MakeEvent());
        _characterRepository.Setup(r => r.GetByIdAsync(CharacterId, default)).ReturnsAsync(MakeCharacter());
        _raidSlotEligibilityValidator.Setup(v => v.ValidateRosterMembershipAsync(CharacterId, GuildBranchId, default)).ReturnsAsync(Result<bool>.Ok(true));
        _raidSlotEligibilityValidator.Setup(v => v.ValidateAssignabilityAsync(It.IsAny<RaidEvent>(), It.IsAny<Character>(), GuildId, GuildBranchId, default)).ReturnsAsync(Result<bool>.Ok(true));
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
        _raidSlotEligibilityValidator.Setup(v => v.ValidateRosterMembershipAsync(CharacterId, GuildBranchId, default))
            .ReturnsAsync(Result<bool>.Fail(ResponseDetail.CharacterNotOnRoster, "Character is not an active member of this guild branch's roster."));

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
        _raidSlotEligibilityValidator.Setup(v => v.ValidateAssignabilityAsync(It.IsAny<RaidEvent>(), It.IsAny<Character>(), GuildId, GuildBranchId, default))
            .ReturnsAsync(Result<bool>.Fail(ResponseDetail.MemberDeclaredAbsent, "This member's declared availability does not cover the event's start time."));

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.MemberDeclaredAbsent);
        _raidCompositionRepository.Verify(r => r.AssignCharacterAsync(It.IsAny<RaidSlotAssignment>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_LockoutConflictDetected_ReturnsRaidLockoutConflictWithZoneName()
    {
        _raidSlotEligibilityValidator.Setup(v => v.ValidateAssignabilityAsync(It.IsAny<RaidEvent>(), It.IsAny<Character>(), GuildId, GuildBranchId, default))
            .ReturnsAsync(Result<bool>.Fail(ResponseDetail.RaidLockoutConflict, "Character is already locked to 'Zul'Gurub' for this reset window via another event."));

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

    // ── Draft vs Published notification gating ──────────────────────────────

    [Fact]
    public async Task HandleAsync_DraftEvent_Succeeds_ButDoesNotNotify()
    {
        // MakeEvent leaves PublicationStatus at its Draft default.
        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _raidCompositionNotifier.Verify(n => n.NotifySlotAssignedAsync(
            It.IsAny<RaidEvent>(), It.IsAny<string>(), It.IsAny<RaidCharacterRef>(), It.IsAny<string>(), It.IsAny<SlotCoordinate>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PublishedEvent_NotifiesWithResolvedCharacterClassAndSpec()
    {
        var publishedEvent = MakeEvent();
        publishedEvent.PublicationStatus = RaidPublicationStatus.Published;
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(publishedEvent);
        _characterRepository.Setup(r => r.GetByIdAsync(CharacterId, default)).ReturnsAsync(new Character
        {
            Id = CharacterId,
            Name = "Arthas",
            ClassId = 6,
            UserDiscordId = PlayerDiscordId,
            IsActiveInRaidOps = true,
        });
        _characterRepository.Setup(r => r.GetRaidSpecsAsync(CharacterId, default)).ReturnsAsync(
        [
            new CharacterRaidSpec { CharacterId = CharacterId, SpecId = 1, IsMain = true, Spec = new Spec { Id = 1, Name = "Blood" } },
        ]);

        var result = await _sut.HandleAsync(MakeCommand(groupNumber: 2, slotNumber: 3));

        result.IsSuccess.Should().BeTrue();
        _raidCompositionNotifier.Verify(n => n.NotifySlotAssignedAsync(
            publishedEvent, RequesterId,
            It.Is<RaidCharacterRef>(c => c.Name == "Arthas" && c.ClassId == 6 && c.SpecName == "Blood"),
            PlayerDiscordId,
            new SlotCoordinate(2, 3),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PublishedEventRepositionedCharacterSpecNoLongerDeclared_NotifiesWithNullSpecName()
    {
        // Repositioning keeps the existing assignment's spec (77) without re-querying raid specs
        // (see HandleAsync_RepositioningWithinSameEvent_KeepsExistingSpecInsteadOfMainSpec) — but
        // the notify block re-fetches raid specs fresh, and the character may no longer declare
        // that spec as raid-viable by the time this runs.
        var publishedEvent = MakeEvent(
            assignments: [new RaidSlotAssignment { GroupNumber = 1, SlotNumber = 1, CharacterId = CharacterId, SpecId = 77, AssignedPlayerDiscordId = PlayerDiscordId }]);
        publishedEvent.PublicationStatus = RaidPublicationStatus.Published;
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(publishedEvent);
        _characterRepository.Setup(r => r.GetRaidSpecsAsync(CharacterId, default)).ReturnsAsync(
        [
            new CharacterRaidSpec { CharacterId = CharacterId, SpecId = 1, IsMain = true, Spec = new Spec { Id = 1, Name = "Blood" } },
        ]);

        var result = await _sut.HandleAsync(MakeCommand(groupNumber: 1, slotNumber: 2));

        result.IsSuccess.Should().BeTrue();
        _raidCompositionNotifier.Verify(n => n.NotifySlotAssignedAsync(
            publishedEvent, RequesterId,
            It.Is<RaidCharacterRef>(c => c.SpecName == null),
            PlayerDiscordId,
            new SlotCoordinate(1, 2),
            default), Times.Once);
    }
}
