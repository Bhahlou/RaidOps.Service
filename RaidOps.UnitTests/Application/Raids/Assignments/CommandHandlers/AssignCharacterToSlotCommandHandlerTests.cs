using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RaidOps.Application.Contracts.Calendar.Availability.Responses;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Assignments.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Assignments.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Calendar;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Assignments.CommandHandlers;

public class AssignCharacterToSlotCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IGuildsRepository> _guildsRepository = new();
    private readonly Mock<IGuildBranchesRepository> _guildBranchesRepository = new();
    private readonly Mock<IRaidEventRepository> _raidEventRepository = new();
    private readonly Mock<ICharacterRepository> _characterRepository = new();
    private readonly Mock<IGuildMembershipRepository> _guildMembershipRepository = new();
    private readonly Mock<IAvailabilityRepository> _availabilityRepository = new();
    private readonly Mock<IAvailabilityResolutionService> _availabilityResolutionService = new();
    private readonly Mock<IRaidZoneRepository> _raidZoneRepository = new();
    private readonly Mock<IWeeklyLockoutScheduleRepository> _weeklyLockoutScheduleRepository = new();
    private readonly Mock<IRaidLockoutService> _raidLockoutService = new();
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
            _access.Object, _guildsRepository.Object, _guildBranchesRepository.Object, _raidEventRepository.Object,
            _characterRepository.Object, _guildMembershipRepository.Object, _availabilityRepository.Object,
            _availabilityResolutionService.Object, _raidZoneRepository.Object, _weeklyLockoutScheduleRepository.Object,
            _raidLockoutService.Object, _raidCompositionRepository.Object,
            new Mock<ILogger<AssignCharacterToSlotCommandHandler>>().Object);

        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(MakeEvent());
        _characterRepository.Setup(r => r.GetByIdAsync(CharacterId, default)).ReturnsAsync(MakeCharacter());
        _guildMembershipRepository.Setup(r => r.GetByCharacterIdAsync(CharacterId, default))
            .ReturnsAsync([new GuildMembership { CharacterId = CharacterId, GuildId = GuildId, GuildBranchId = GuildBranchId }]);
        _guildsRepository.Setup(r => r.GetByIdAsync(GuildId, default)).ReturnsAsync(new Guild { Id = GuildId, Timezone = null });
        _availabilityRepository.Setup(r => r.GetExceptionsOverlappingAsync(PlayerDiscordId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), default)).ReturnsAsync([]);
        _availabilityRepository.Setup(r => r.GetPatternsAsync(PlayerDiscordId, default)).ReturnsAsync([]);
        _availabilityResolutionService.Setup(s => s.ResolveForScope(
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<IReadOnlyCollection<AvailabilityDeclaration>>(),
                It.IsAny<IReadOnlyCollection<RecurringAvailabilityPattern>>(), GuildId, GuildBranchId))
            .Returns([new ResolvedDayAvailabilityResponse { Status = DayAvailabilityStatus.Available }]);
        _characterRepository.Setup(r => r.GetRaidSpecsAsync(CharacterId, default))
            .ReturnsAsync([new CharacterRaidSpec { CharacterId = CharacterId, SpecId = 1, IsMain = true }]);
        _raidCompositionRepository.Setup(r => r.GetActiveAssignmentsForCharacterInGuildBranchAsync(CharacterId, GuildBranchId, default)).ReturnsAsync([]);
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

    private void SetupNotFullyAvailable(DayAvailabilityStatus status, TimeOnly? from = null, TimeOnly? until = null) =>
        _availabilityResolutionService.Setup(s => s.ResolveForScope(
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<IReadOnlyCollection<AvailabilityDeclaration>>(),
                It.IsAny<IReadOnlyCollection<RecurringAvailabilityPattern>>(), GuildId, GuildBranchId))
            .Returns([new ResolvedDayAvailabilityResponse { Status = status, AvailableFrom = from, AvailableUntil = until }]);

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
    public async Task HandleAsync_DeclaredAbsent_ReturnsMemberDeclaredAbsent()
    {
        SetupNotFullyAvailable(DayAvailabilityStatus.Absent);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.MemberDeclaredAbsent);
    }

    [Fact]
    public async Task HandleAsync_PartialAvailabilityOutsideWindow_ReturnsMemberDeclaredAbsent()
    {
        // Event starts at 20:00 UTC; member is only available until 18:00.
        SetupNotFullyAvailable(DayAvailabilityStatus.Partial, until: new TimeOnly(18, 0));

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.MemberDeclaredAbsent);
    }

    [Fact]
    public async Task HandleAsync_PartialAvailabilityWithinWindow_Succeeds()
    {
        SetupNotFullyAvailable(DayAvailabilityStatus.Partial, from: new TimeOnly(18, 0), until: new TimeOnly(23, 0));

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_NoLockoutConflictNoZones_NewAssignmentDefaultsToMainSpec()
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

    // ── Lockout conflict checks ─────────────────────────────────────────────

    private static readonly DateTime LockoutAnchor = new(2026, 1, 1, 4, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task HandleAsync_ZoneWithIndependentCadence_SameWindowAsOtherEvent_ReturnsRaidLockoutConflict()
    {
        var targetEvent = MakeEvent(targetZones: [new RaidEventZone { RaidZoneId = 7 }]);
        targetEvent.StartsAtUtc = LockoutAnchor.AddDays(5);
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(targetEvent);

        var zone = new RaidZone { Id = 7, Name = "Zul'Gurub", LockoutCadenceDays = 3, LockoutAnchorUtc = LockoutAnchor };
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId });
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.Is<IEnumerable<int>>(ids => ids.Contains(7)), default)).ReturnsAsync([zone]);
        _raidZoneRepository.Setup(r => r.GetGuildOverridesAsync(GuildId, It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([]);

        var otherEvent = MakeEvent(targetZones: [new RaidEventZone { RaidZoneId = 7 }]);
        otherEvent.Id = EventId + 1;
        otherEvent.StartsAtUtc = LockoutAnchor.AddDays(4);
        _raidCompositionRepository.Setup(r => r.GetActiveAssignmentsForCharacterInGuildBranchAsync(CharacterId, GuildBranchId, default))
            .ReturnsAsync([new RaidSlotAssignment { RaidEventId = otherEvent.Id, RaidEvent = otherEvent }]);

        _raidLockoutService.Setup(s => s.GetLockoutWindowStart(LockoutAnchor, 3, It.IsAny<IReadOnlyCollection<RaidLockoutCadenceOverride>>(), targetEvent.StartsAtUtc))
            .Returns(LockoutAnchor.AddDays(3));
        _raidLockoutService.Setup(s => s.GetLockoutWindowStart(LockoutAnchor, 3, It.IsAny<IReadOnlyCollection<RaidLockoutCadenceOverride>>(), otherEvent.StartsAtUtc))
            .Returns(LockoutAnchor.AddDays(3));

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidLockoutConflict);
        _raidCompositionRepository.Verify(r => r.AssignCharacterAsync(It.IsAny<RaidSlotAssignment>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ZoneWithIndependentCadence_DifferentWindowThanOtherEvent_Succeeds()
    {
        var targetEvent = MakeEvent(targetZones: [new RaidEventZone { RaidZoneId = 7 }]);
        targetEvent.StartsAtUtc = LockoutAnchor.AddDays(5);
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(targetEvent);

        var zone = new RaidZone { Id = 7, Name = "Zul'Gurub", LockoutCadenceDays = 3, LockoutAnchorUtc = LockoutAnchor };
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId });
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.Is<IEnumerable<int>>(ids => ids.Contains(7)), default)).ReturnsAsync([zone]);
        _raidZoneRepository.Setup(r => r.GetGuildOverridesAsync(GuildId, It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([]);

        var otherEvent = MakeEvent(targetZones: [new RaidEventZone { RaidZoneId = 7 }]);
        otherEvent.Id = EventId + 1;
        otherEvent.StartsAtUtc = LockoutAnchor.AddDays(10);
        _raidCompositionRepository.Setup(r => r.GetActiveAssignmentsForCharacterInGuildBranchAsync(CharacterId, GuildBranchId, default))
            .ReturnsAsync([new RaidSlotAssignment { RaidEventId = otherEvent.Id, RaidEvent = otherEvent }]);

        _raidLockoutService.Setup(s => s.GetLockoutWindowStart(LockoutAnchor, 3, It.IsAny<IReadOnlyCollection<RaidLockoutCadenceOverride>>(), targetEvent.StartsAtUtc))
            .Returns(LockoutAnchor.AddDays(3));
        _raidLockoutService.Setup(s => s.GetLockoutWindowStart(LockoutAnchor, 3, It.IsAny<IReadOnlyCollection<RaidLockoutCadenceOverride>>(), otherEvent.StartsAtUtc))
            .Returns(LockoutAnchor.AddDays(9));

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_OtherAssignmentInSameEvent_ExcludedFromLockoutComparison()
    {
        var targetEvent = MakeEvent(targetZones: [new RaidEventZone { RaidZoneId = 7 }]);
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(targetEvent);

        var zone = new RaidZone { Id = 7, LockoutCadenceDays = 3, LockoutAnchorUtc = LockoutAnchor };
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId });
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([zone]);
        _raidZoneRepository.Setup(r => r.GetGuildOverridesAsync(GuildId, It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([]);

        // The "other" assignment is actually in the same event being assigned to — must be skipped.
        _raidCompositionRepository.Setup(r => r.GetActiveAssignmentsForCharacterInGuildBranchAsync(CharacterId, GuildBranchId, default))
            .ReturnsAsync([new RaidSlotAssignment { RaidEventId = EventId, RaidEvent = targetEvent }]);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _raidLockoutService.Verify(s => s.GetLockoutWindowStart(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<IReadOnlyCollection<RaidLockoutCadenceOverride>>(), It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_OtherEventDoesNotTargetSharedZone_NoConflict()
    {
        var targetEvent = MakeEvent(targetZones: [new RaidEventZone { RaidZoneId = 7 }]);
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(targetEvent);

        var zone = new RaidZone { Id = 7, LockoutCadenceDays = 3, LockoutAnchorUtc = LockoutAnchor };
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId });
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([zone]);
        _raidZoneRepository.Setup(r => r.GetGuildOverridesAsync(GuildId, It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([]);

        var otherEvent = MakeEvent(targetZones: [new RaidEventZone { RaidZoneId = 99 }]);
        otherEvent.Id = EventId + 1;
        _raidCompositionRepository.Setup(r => r.GetActiveAssignmentsForCharacterInGuildBranchAsync(CharacterId, GuildBranchId, default))
            .ReturnsAsync([new RaidSlotAssignment { RaidEventId = otherEvent.Id, RaidEvent = otherEvent }]);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_NoBaselineResolvable_SoftSkipsLockoutCheckAndSucceeds()
    {
        var targetEvent = MakeEvent(targetZones: [new RaidEventZone { RaidZoneId = 7 }]);
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(targetEvent);

        // Zone has no independent cadence, and the branch has no region configured.
        var zone = new RaidZone { Id = 7, LockoutCadenceDays = null, LockoutAnchorUtc = null };
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId, Region = null });
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([zone]);
        _raidZoneRepository.Setup(r => r.GetGuildOverridesAsync(GuildId, It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([]);
        _raidCompositionRepository.Setup(r => r.GetActiveAssignmentsForCharacterInGuildBranchAsync(CharacterId, GuildBranchId, default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _raidLockoutService.Verify(s => s.GetLockoutWindowStart(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<IReadOnlyCollection<RaidLockoutCadenceOverride>>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ZoneFollowsRegionSchedule_WithNoScheduleSeeded_SoftSkips()
    {
        var targetEvent = MakeEvent(targetZones: [new RaidEventZone { RaidZoneId = 7 }]);
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(targetEvent);

        var zone = new RaidZone { Id = 7, LockoutCadenceDays = null, LockoutAnchorUtc = null };
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId, Region = "eu" });
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([zone]);
        _raidZoneRepository.Setup(r => r.GetGuildOverridesAsync(GuildId, It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([]);
        _weeklyLockoutScheduleRepository.Setup(r => r.GetByRegionAsync("eu", default)).ReturnsAsync((WeeklyLockoutSchedule?)null);
        _raidCompositionRepository.Setup(r => r.GetActiveAssignmentsForCharacterInGuildBranchAsync(CharacterId, GuildBranchId, default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_ZoneFollowsRegionSchedule_ConflictDetectedUsingScheduleBaseline()
    {
        var targetEvent = MakeEvent(targetZones: [new RaidEventZone { RaidZoneId = 7 }]);
        targetEvent.StartsAtUtc = LockoutAnchor.AddDays(5);
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(targetEvent);

        var zone = new RaidZone { Id = 7, LockoutCadenceDays = null, LockoutAnchorUtc = null };
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId, Region = "eu" });
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([zone]);
        _raidZoneRepository.Setup(r => r.GetGuildOverridesAsync(GuildId, It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([]);
        _weeklyLockoutScheduleRepository.Setup(r => r.GetByRegionAsync("eu", default)).ReturnsAsync(new WeeklyLockoutSchedule { Region = "eu", AnchorUtc = LockoutAnchor, CadenceDays = 7 });

        var otherEvent = MakeEvent(targetZones: [new RaidEventZone { RaidZoneId = 7 }]);
        otherEvent.Id = EventId + 1;
        otherEvent.StartsAtUtc = LockoutAnchor.AddDays(6);
        _raidCompositionRepository.Setup(r => r.GetActiveAssignmentsForCharacterInGuildBranchAsync(CharacterId, GuildBranchId, default))
            .ReturnsAsync([new RaidSlotAssignment { RaidEventId = otherEvent.Id, RaidEvent = otherEvent }]);

        _raidLockoutService.Setup(s => s.GetLockoutWindowStart(LockoutAnchor, 7, It.IsAny<IReadOnlyCollection<RaidLockoutCadenceOverride>>(), targetEvent.StartsAtUtc))
            .Returns(LockoutAnchor);
        _raidLockoutService.Setup(s => s.GetLockoutWindowStart(LockoutAnchor, 7, It.IsAny<IReadOnlyCollection<RaidLockoutCadenceOverride>>(), otherEvent.StartsAtUtc))
            .Returns(LockoutAnchor);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidLockoutConflict);
    }

    [Fact]
    public async Task HandleAsync_GuildOverrideCorrectsBaseline_UsedInsteadOfZoneCadence()
    {
        var targetEvent = MakeEvent(targetZones: [new RaidEventZone { RaidZoneId = 7 }]);
        targetEvent.StartsAtUtc = LockoutAnchor.AddDays(5);
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(targetEvent);

        var zone = new RaidZone { Id = 7, LockoutCadenceDays = 3, LockoutAnchorUtc = LockoutAnchor };
        var guildOverride = new GuildRaidZoneLockout { GuildId = GuildId, RaidZoneId = 7, LockoutCadenceDays = 10, LockoutAnchorUtc = null };
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId });
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([zone]);
        _raidZoneRepository.Setup(r => r.GetGuildOverridesAsync(GuildId, It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([guildOverride]);
        _raidCompositionRepository.Setup(r => r.GetActiveAssignmentsForCharacterInGuildBranchAsync(CharacterId, GuildBranchId, default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        // Override's cadence (10) must be the one passed through, not the zone's own (3).
        _raidLockoutService.Verify(s => s.GetLockoutWindowStart(LockoutAnchor, 10, It.IsAny<IReadOnlyCollection<RaidLockoutCadenceOverride>>(), targetEvent.StartsAtUtc), Times.Once);
    }
}
