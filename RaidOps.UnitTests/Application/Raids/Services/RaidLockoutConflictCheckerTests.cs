using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Services;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Services;

public class RaidLockoutConflictCheckerTests
{
    private readonly Mock<IGuildBranchesRepository> _guildBranchesRepository = new();
    private readonly Mock<IRaidZoneRepository> _raidZoneRepository = new();
    private readonly Mock<IWeeklyLockoutScheduleRepository> _weeklyLockoutScheduleRepository = new();
    private readonly Mock<IRaidLockoutService> _raidLockoutService = new();
    private readonly Mock<IRaidCompositionRepository> _raidCompositionRepository = new();
    private readonly RaidLockoutConflictChecker _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const int CharacterId = 42;
    private const int EventId = 5;

    private static readonly DateTime LockoutAnchor = new(2026, 1, 1, 4, 0, 0, DateTimeKind.Utc);

    public RaidLockoutConflictCheckerTests()
    {
        _sut = new RaidLockoutConflictChecker(
            _guildBranchesRepository.Object, _raidZoneRepository.Object, _weeklyLockoutScheduleRepository.Object,
            _raidLockoutService.Object, _raidCompositionRepository.Object, new Mock<ILogger<RaidLockoutConflictChecker>>().Object);

        _raidCompositionRepository.Setup(r => r.GetActiveAssignmentsForCharacterInGuildBranchAsync(CharacterId, GuildBranchId, default)).ReturnsAsync([]);
        _raidZoneRepository.Setup(r => r.GetGuildOverridesAsync(GuildId, It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([]);
    }

    private static RaidEvent MakeEvent(int id, List<RaidEventZone> targetZones, DateTime? startsAtUtc = null) => new()
    {
        Id = id,
        StartsAtUtc = startsAtUtc ?? LockoutAnchor.AddDays(5),
        TargetZones = targetZones,
    };

    [Fact]
    public async Task FindConflictingZoneNameAsync_NoTargetZones_ReturnsNull()
    {
        var raidEvent = MakeEvent(EventId, targetZones: []);

        var result = await _sut.FindConflictingZoneNameAsync(raidEvent, CharacterId, GuildId, GuildBranchId);

        result.Should().BeNull();
        _guildBranchesRepository.Verify(r => r.GetByIdAsync(It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task FindConflictingZoneNameAsync_ZoneWithIndependentCadence_SameWindowAsOtherEvent_ReturnsZoneName()
    {
        var targetEvent = MakeEvent(EventId, [new RaidEventZone { RaidZoneId = 7 }], LockoutAnchor.AddDays(5));
        var zone = new RaidZone { Id = 7, Name = "Zul'Gurub", LockoutCadenceDays = 3, LockoutAnchorUtc = LockoutAnchor };
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId });
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.Is<IEnumerable<int>>(ids => ids.Contains(7)), default)).ReturnsAsync([zone]);

        var otherEvent = MakeEvent(EventId + 1, [new RaidEventZone { RaidZoneId = 7 }], LockoutAnchor.AddDays(4));
        _raidCompositionRepository.Setup(r => r.GetActiveAssignmentsForCharacterInGuildBranchAsync(CharacterId, GuildBranchId, default))
            .ReturnsAsync([new RaidSlotAssignment { RaidEventId = otherEvent.Id, RaidEvent = otherEvent }]);

        _raidLockoutService.Setup(s => s.GetLockoutWindowStart(LockoutAnchor, 3, It.IsAny<IReadOnlyCollection<RaidLockoutCadenceOverride>>(), targetEvent.StartsAtUtc))
            .Returns(LockoutAnchor.AddDays(3));
        _raidLockoutService.Setup(s => s.GetLockoutWindowStart(LockoutAnchor, 3, It.IsAny<IReadOnlyCollection<RaidLockoutCadenceOverride>>(), otherEvent.StartsAtUtc))
            .Returns(LockoutAnchor.AddDays(3));

        var result = await _sut.FindConflictingZoneNameAsync(targetEvent, CharacterId, GuildId, GuildBranchId);

        result.Should().Be("Zul'Gurub");
    }

    [Fact]
    public async Task FindConflictingZoneNameAsync_ZoneWithIndependentCadence_DifferentWindowThanOtherEvent_ReturnsNull()
    {
        var targetEvent = MakeEvent(EventId, [new RaidEventZone { RaidZoneId = 7 }], LockoutAnchor.AddDays(5));
        var zone = new RaidZone { Id = 7, LockoutCadenceDays = 3, LockoutAnchorUtc = LockoutAnchor };
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId });
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([zone]);

        var otherEvent = MakeEvent(EventId + 1, [new RaidEventZone { RaidZoneId = 7 }], LockoutAnchor.AddDays(10));
        _raidCompositionRepository.Setup(r => r.GetActiveAssignmentsForCharacterInGuildBranchAsync(CharacterId, GuildBranchId, default))
            .ReturnsAsync([new RaidSlotAssignment { RaidEventId = otherEvent.Id, RaidEvent = otherEvent }]);

        _raidLockoutService.Setup(s => s.GetLockoutWindowStart(LockoutAnchor, 3, It.IsAny<IReadOnlyCollection<RaidLockoutCadenceOverride>>(), targetEvent.StartsAtUtc))
            .Returns(LockoutAnchor.AddDays(3));
        _raidLockoutService.Setup(s => s.GetLockoutWindowStart(LockoutAnchor, 3, It.IsAny<IReadOnlyCollection<RaidLockoutCadenceOverride>>(), otherEvent.StartsAtUtc))
            .Returns(LockoutAnchor.AddDays(9));

        var result = await _sut.FindConflictingZoneNameAsync(targetEvent, CharacterId, GuildId, GuildBranchId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task FindConflictingZoneNameAsync_OtherAssignmentInSameEvent_ExcludedFromComparison()
    {
        var targetEvent = MakeEvent(EventId, [new RaidEventZone { RaidZoneId = 7 }]);
        var zone = new RaidZone { Id = 7, LockoutCadenceDays = 3, LockoutAnchorUtc = LockoutAnchor };
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId });
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([zone]);

        // The "other" assignment is actually in the same event being checked — must be skipped.
        _raidCompositionRepository.Setup(r => r.GetActiveAssignmentsForCharacterInGuildBranchAsync(CharacterId, GuildBranchId, default))
            .ReturnsAsync([new RaidSlotAssignment { RaidEventId = EventId, RaidEvent = targetEvent }]);

        var result = await _sut.FindConflictingZoneNameAsync(targetEvent, CharacterId, GuildId, GuildBranchId);

        result.Should().BeNull();
        _raidLockoutService.Verify(s => s.GetLockoutWindowStart(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<IReadOnlyCollection<RaidLockoutCadenceOverride>>(), It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task FindConflictingZoneNameAsync_OtherEventDoesNotTargetSharedZone_ReturnsNull()
    {
        var targetEvent = MakeEvent(EventId, [new RaidEventZone { RaidZoneId = 7 }]);
        var zone = new RaidZone { Id = 7, LockoutCadenceDays = 3, LockoutAnchorUtc = LockoutAnchor };
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId });
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([zone]);

        var otherEvent = MakeEvent(EventId + 1, [new RaidEventZone { RaidZoneId = 99 }]);
        _raidCompositionRepository.Setup(r => r.GetActiveAssignmentsForCharacterInGuildBranchAsync(CharacterId, GuildBranchId, default))
            .ReturnsAsync([new RaidSlotAssignment { RaidEventId = otherEvent.Id, RaidEvent = otherEvent }]);

        var result = await _sut.FindConflictingZoneNameAsync(targetEvent, CharacterId, GuildId, GuildBranchId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task FindConflictingZoneNameAsync_NoBaselineResolvable_SoftSkipsAndReturnsNull()
    {
        var targetEvent = MakeEvent(EventId, [new RaidEventZone { RaidZoneId = 7 }]);
        var zone = new RaidZone { Id = 7, LockoutCadenceDays = null, LockoutAnchorUtc = null };
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId, Region = null });
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([zone]);

        var result = await _sut.FindConflictingZoneNameAsync(targetEvent, CharacterId, GuildId, GuildBranchId);

        result.Should().BeNull();
        _raidLockoutService.Verify(s => s.GetLockoutWindowStart(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<IReadOnlyCollection<RaidLockoutCadenceOverride>>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task FindConflictingZoneNameAsync_GuildBranchNotFound_SoftSkipsAndReturnsNull()
    {
        // Distinct from a resolved branch with no Region: the branch lookup itself returns null
        // (e.g. a stale/deleted branch), exercising the "guildBranch?.Region" null-conditional's
        // own null path rather than just its Region being null.
        var targetEvent = MakeEvent(EventId, [new RaidEventZone { RaidZoneId = 7 }]);
        var zone = new RaidZone { Id = 7, LockoutCadenceDays = null, LockoutAnchorUtc = null };
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync((GuildBranch?)null);
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([zone]);

        var result = await _sut.FindConflictingZoneNameAsync(targetEvent, CharacterId, GuildId, GuildBranchId);

        result.Should().BeNull();
        _raidLockoutService.Verify(s => s.GetLockoutWindowStart(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<IReadOnlyCollection<RaidLockoutCadenceOverride>>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task FindConflictingZoneNameAsync_RegionScheduleNotSeeded_SoftSkipsAndReturnsNull()
    {
        var targetEvent = MakeEvent(EventId, [new RaidEventZone { RaidZoneId = 7 }]);
        var zone = new RaidZone { Id = 7, LockoutCadenceDays = null, LockoutAnchorUtc = null };
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId, Region = "eu" });
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([zone]);
        _weeklyLockoutScheduleRepository.Setup(r => r.GetByRegionAsync("eu", default)).ReturnsAsync((WeeklyLockoutSchedule?)null);

        var result = await _sut.FindConflictingZoneNameAsync(targetEvent, CharacterId, GuildId, GuildBranchId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task FindConflictingZoneNameAsync_ZoneFollowsRegionSchedule_ConflictDetectedUsingScheduleBaseline()
    {
        var targetEvent = MakeEvent(EventId, [new RaidEventZone { RaidZoneId = 7 }], LockoutAnchor.AddDays(5));
        var zone = new RaidZone { Id = 7, LockoutCadenceDays = null, LockoutAnchorUtc = null };
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId, Region = "eu" });
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([zone]);
        _weeklyLockoutScheduleRepository.Setup(r => r.GetByRegionAsync("eu", default)).ReturnsAsync(new WeeklyLockoutSchedule { Region = "eu", AnchorUtc = LockoutAnchor, CadenceDays = 7 });

        var otherEvent = MakeEvent(EventId + 1, [new RaidEventZone { RaidZoneId = 7 }], LockoutAnchor.AddDays(6));
        _raidCompositionRepository.Setup(r => r.GetActiveAssignmentsForCharacterInGuildBranchAsync(CharacterId, GuildBranchId, default))
            .ReturnsAsync([new RaidSlotAssignment { RaidEventId = otherEvent.Id, RaidEvent = otherEvent }]);

        _raidLockoutService.Setup(s => s.GetLockoutWindowStart(LockoutAnchor, 7, It.IsAny<IReadOnlyCollection<RaidLockoutCadenceOverride>>(), targetEvent.StartsAtUtc))
            .Returns(LockoutAnchor);
        _raidLockoutService.Setup(s => s.GetLockoutWindowStart(LockoutAnchor, 7, It.IsAny<IReadOnlyCollection<RaidLockoutCadenceOverride>>(), otherEvent.StartsAtUtc))
            .Returns(LockoutAnchor);

        var result = await _sut.FindConflictingZoneNameAsync(targetEvent, CharacterId, GuildId, GuildBranchId);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task FindConflictingZoneNameAsync_GuildOverrideCorrectsBaseline_UsedInsteadOfZoneCadence()
    {
        var targetEvent = MakeEvent(EventId, [new RaidEventZone { RaidZoneId = 7 }], LockoutAnchor.AddDays(5));
        var zone = new RaidZone { Id = 7, LockoutCadenceDays = 3, LockoutAnchorUtc = LockoutAnchor };
        var guildOverride = new GuildRaidZoneLockout { GuildId = GuildId, RaidZoneId = 7, LockoutCadenceDays = 10, LockoutAnchorUtc = null };
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId });
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([zone]);
        _raidZoneRepository.Setup(r => r.GetGuildOverridesAsync(GuildId, It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([guildOverride]);

        var result = await _sut.FindConflictingZoneNameAsync(targetEvent, CharacterId, GuildId, GuildBranchId);

        result.Should().BeNull();
        // Override's cadence (10) must be the one passed through, not the zone's own (3).
        _raidLockoutService.Verify(s => s.GetLockoutWindowStart(LockoutAnchor, 10, It.IsAny<IReadOnlyCollection<RaidLockoutCadenceOverride>>(), targetEvent.StartsAtUtc), Times.Once);
    }

    // ── Extension chain exemption ────────────────────────────────────────────

    [Fact]
    public async Task FindConflictingZoneNameAsync_OtherEventExtendsTargetEvent_SameWindow_ReturnsNull()
    {
        var targetEvent = MakeEvent(EventId, [new RaidEventZone { RaidZoneId = 7 }], LockoutAnchor.AddDays(5));
        var zone = new RaidZone { Id = 7, LockoutCadenceDays = 3, LockoutAnchorUtc = LockoutAnchor };
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId });
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([zone]);

        // Other event extends the target event directly — same lockout window, but that's intentional.
        var otherEvent = MakeEvent(EventId + 1, [new RaidEventZone { RaidZoneId = 7 }], LockoutAnchor.AddDays(4));
        otherEvent.ExtendsRaidEventId = targetEvent.Id;
        _raidCompositionRepository.Setup(r => r.GetActiveAssignmentsForCharacterInGuildBranchAsync(CharacterId, GuildBranchId, default))
            .ReturnsAsync([new RaidSlotAssignment { RaidEventId = otherEvent.Id, RaidEvent = otherEvent }]);

        _raidLockoutService.Setup(s => s.GetLockoutWindowStart(LockoutAnchor, 3, It.IsAny<IReadOnlyCollection<RaidLockoutCadenceOverride>>(), It.IsAny<DateTime>()))
            .Returns(LockoutAnchor.AddDays(3));

        var result = await _sut.FindConflictingZoneNameAsync(targetEvent, CharacterId, GuildId, GuildBranchId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task FindConflictingZoneNameAsync_TargetEventExtendsOtherEvent_SameWindow_ReturnsNull()
    {
        // Same relationship, reversed direction: the event being checked is the one that extends
        // the other, not the other way around.
        var targetEvent = MakeEvent(EventId, [new RaidEventZone { RaidZoneId = 7 }], LockoutAnchor.AddDays(5));
        targetEvent.ExtendsRaidEventId = EventId + 1;
        var zone = new RaidZone { Id = 7, LockoutCadenceDays = 3, LockoutAnchorUtc = LockoutAnchor };
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId });
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([zone]);

        var otherEvent = MakeEvent(EventId + 1, [new RaidEventZone { RaidZoneId = 7 }], LockoutAnchor.AddDays(4));
        _raidCompositionRepository.Setup(r => r.GetActiveAssignmentsForCharacterInGuildBranchAsync(CharacterId, GuildBranchId, default))
            .ReturnsAsync([new RaidSlotAssignment { RaidEventId = otherEvent.Id, RaidEvent = otherEvent }]);

        _raidLockoutService.Setup(s => s.GetLockoutWindowStart(LockoutAnchor, 3, It.IsAny<IReadOnlyCollection<RaidLockoutCadenceOverride>>(), It.IsAny<DateTime>()))
            .Returns(LockoutAnchor.AddDays(3));

        var result = await _sut.FindConflictingZoneNameAsync(targetEvent, CharacterId, GuildId, GuildBranchId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task FindConflictingZoneNameAsync_BothEventsExtendSameRoot_SameWindow_ReturnsNull()
    {
        // Siblings in a 3-night chain, both flattened to point at the same root — neither is the
        // other's direct target, but they still share a chain and must not conflict.
        var targetEvent = MakeEvent(EventId, [new RaidEventZone { RaidZoneId = 7 }], LockoutAnchor.AddDays(5));
        targetEvent.ExtendsRaidEventId = 1;
        var zone = new RaidZone { Id = 7, LockoutCadenceDays = 3, LockoutAnchorUtc = LockoutAnchor };
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId });
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([zone]);

        var otherEvent = MakeEvent(EventId + 1, [new RaidEventZone { RaidZoneId = 7 }], LockoutAnchor.AddDays(4));
        otherEvent.ExtendsRaidEventId = 1;
        _raidCompositionRepository.Setup(r => r.GetActiveAssignmentsForCharacterInGuildBranchAsync(CharacterId, GuildBranchId, default))
            .ReturnsAsync([new RaidSlotAssignment { RaidEventId = otherEvent.Id, RaidEvent = otherEvent }]);

        _raidLockoutService.Setup(s => s.GetLockoutWindowStart(LockoutAnchor, 3, It.IsAny<IReadOnlyCollection<RaidLockoutCadenceOverride>>(), It.IsAny<DateTime>()))
            .Returns(LockoutAnchor.AddDays(3));

        var result = await _sut.FindConflictingZoneNameAsync(targetEvent, CharacterId, GuildId, GuildBranchId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task FindConflictingZoneNameAsync_UnrelatedEvents_SameWindow_StillReturnsZoneName()
    {
        // Regression guard: the extension-chain exemption must not accidentally swallow genuine
        // conflicts between two events that have nothing to do with each other.
        var targetEvent = MakeEvent(EventId, [new RaidEventZone { RaidZoneId = 7 }], LockoutAnchor.AddDays(5));
        var zone = new RaidZone { Id = 7, Name = "Zul'Gurub", LockoutCadenceDays = 3, LockoutAnchorUtc = LockoutAnchor };
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId });
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([zone]);

        var otherEvent = MakeEvent(EventId + 1, [new RaidEventZone { RaidZoneId = 7 }], LockoutAnchor.AddDays(4));
        _raidCompositionRepository.Setup(r => r.GetActiveAssignmentsForCharacterInGuildBranchAsync(CharacterId, GuildBranchId, default))
            .ReturnsAsync([new RaidSlotAssignment { RaidEventId = otherEvent.Id, RaidEvent = otherEvent }]);

        _raidLockoutService.Setup(s => s.GetLockoutWindowStart(LockoutAnchor, 3, It.IsAny<IReadOnlyCollection<RaidLockoutCadenceOverride>>(), It.IsAny<DateTime>()))
            .Returns(LockoutAnchor.AddDays(3));

        var result = await _sut.FindConflictingZoneNameAsync(targetEvent, CharacterId, GuildId, GuildBranchId);

        result.Should().Be("Zul'Gurub");
    }
}
