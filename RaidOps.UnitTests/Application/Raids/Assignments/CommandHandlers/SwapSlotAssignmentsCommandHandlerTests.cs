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

public class SwapSlotAssignmentsCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IRaidEventRepository> _raidEventRepository = new();
    private readonly Mock<ICharacterRepository> _characterRepository = new();
    private readonly Mock<IRaidCompositionRepository> _compositionRepository = new();
    private readonly Mock<IRaidCompositionNotifier> _raidCompositionNotifier = new();
    private readonly SwapSlotAssignmentsCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "officer-1";
    private const int EventId = 5;

    public SwapSlotAssignmentsCommandHandlerTests()
    {
        _sut = new SwapSlotAssignmentsCommandHandler(
            _access.Object, _raidEventRepository.Object, _characterRepository.Object, _compositionRepository.Object,
            _raidCompositionNotifier.Object);
    }

    private static SwapSlotAssignmentsCommand MakeCommand(int groupA = 1, int slotA = 1, int groupB = 2, int slotB = 2) => new()
    {
        GuildId = GuildId,
        RequesterDiscordId = RequesterId,
        GuildBranchId = GuildBranchId,
        EventId = EventId,
        GroupNumberA = groupA,
        SlotNumberA = slotA,
        GroupNumberB = groupB,
        SlotNumberB = slotB,
    };

    private static RaidEvent MakeEvent(int groupCount = 3, int slotsPerGroup = 3, RaidPublicationStatus status = RaidPublicationStatus.Draft, List<RaidSlotAssignment>? assignments = null) => new()
    {
        Id = EventId,
        GroupCount = groupCount,
        SlotsPerGroup = slotsPerGroup,
        PublicationStatus = status,
        Assignments = assignments ?? [],
    };

    private void SetupOfficer() =>
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);

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
        SetupOfficer();
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync((RaidEvent?)null);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidEventNotFound);
    }

    [Theory]
    [InlineData(0, 1, 1, 1)]
    [InlineData(1, 0, 1, 1)]
    [InlineData(4, 1, 1, 1)]
    [InlineData(1, 4, 1, 1)]
    [InlineData(1, 1, 0, 1)]
    [InlineData(1, 1, 1, 0)]
    [InlineData(1, 1, 4, 1)]
    [InlineData(1, 1, 1, 4)]
    public async Task HandleAsync_CoordinateOutOfGridBounds_ReturnsInvalidGroupOrSlotNumber(int groupA, int slotA, int groupB, int slotB)
    {
        SetupOfficer();
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(MakeEvent());

        var result = await _sut.HandleAsync(MakeCommand(groupA, slotA, groupB, slotB));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidGroupOrSlotNumber);
        _compositionRepository.Verify(r => r.SwapAssignmentsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SameCoordinateBothSides_IsNoOpSuccessWithoutSwapping()
    {
        SetupOfficer();
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(MakeEvent());

        var result = await _sut.HandleAsync(MakeCommand(1, 1, 1, 1));

        result.IsSuccess.Should().BeTrue();
        _compositionRepository.Verify(r => r.SwapAssignmentsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_OneSlotEmpty_ReturnsBothSlotsMustBeOccupiedToSwap()
    {
        SetupOfficer();
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(MakeEvent());
        _compositionRepository.Setup(r => r.SwapAssignmentsAsync(EventId, 1, 1, 2, 2, default)).ReturnsAsync(false);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.BothSlotsMustBeOccupiedToSwap);
    }

    [Fact]
    public async Task HandleAsync_Success_SwapsAndReturnsOk()
    {
        SetupOfficer();
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(MakeEvent());
        _compositionRepository.Setup(r => r.SwapAssignmentsAsync(EventId, 1, 1, 2, 2, default)).ReturnsAsync(true);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _compositionRepository.Verify(r => r.SwapAssignmentsAsync(EventId, 1, 1, 2, 2, default), Times.Once);
    }

    // ── Draft vs Published notification gating ──────────────────────────────

    private const int CharacterAId = 42;
    private const int CharacterBId = 43;

    private static List<RaidSlotAssignment> MakeOccupants() =>
    [
        new RaidSlotAssignment { GroupNumber = 1, SlotNumber = 1, CharacterId = CharacterAId, SpecId = 1 },
        new RaidSlotAssignment { GroupNumber = 2, SlotNumber = 2, CharacterId = CharacterBId, SpecId = 2 },
    ];

    [Fact]
    public async Task HandleAsync_PublishedEventOccupantAAtDifferentGroup_TreatsOccupantAAsNullAndDoesNotNotify()
    {
        // Assignment sits at a different GroupNumber (99 vs 1) than requested for side A, with a
        // matching SlotNumber (1) — the predicate short-circuits to false on the GroupNumber half,
        // a branch never exercised by MakeOccupants()'s exact-match entries.
        SetupOfficer();
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(MakeEvent(status: RaidPublicationStatus.Published, assignments:
        [
            new RaidSlotAssignment { GroupNumber = 99, SlotNumber = 1, CharacterId = CharacterAId, SpecId = 1 },
            new RaidSlotAssignment { GroupNumber = 2, SlotNumber = 2, CharacterId = CharacterBId, SpecId = 2 },
        ]));
        _compositionRepository.Setup(r => r.SwapAssignmentsAsync(EventId, 1, 1, 2, 2, default)).ReturnsAsync(true);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _raidCompositionNotifier.Verify(n => n.NotifySlotsSwappedAsync(
            It.IsAny<RaidEvent>(), It.IsAny<string>(), It.IsAny<RaidCharacterRef>(), It.IsAny<SlotCoordinate>(), It.IsAny<RaidCharacterRef>(), It.IsAny<SlotCoordinate>(), default),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_DraftEventBothSlotsOccupied_Succeeds_ButDoesNotNotify()
    {
        SetupOfficer();
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(MakeEvent(assignments: MakeOccupants()));
        _compositionRepository.Setup(r => r.SwapAssignmentsAsync(EventId, 1, 1, 2, 2, default)).ReturnsAsync(true);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _raidCompositionNotifier.Verify(n => n.NotifySlotsSwappedAsync(
            It.IsAny<RaidEvent>(), It.IsAny<string>(), It.IsAny<RaidCharacterRef>(), It.IsAny<SlotCoordinate>(), It.IsAny<RaidCharacterRef>(), It.IsAny<SlotCoordinate>(), default),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PublishedEventBothSlotsOccupied_NotifiesWithBothResolvedCharacters()
    {
        SetupOfficer();
        var publishedEvent = MakeEvent(status: RaidPublicationStatus.Published, assignments: MakeOccupants());
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(publishedEvent);
        _compositionRepository.Setup(r => r.SwapAssignmentsAsync(EventId, 1, 1, 2, 2, default)).ReturnsAsync(true);
        _characterRepository.Setup(r => r.GetByIdAsync(CharacterAId, default)).ReturnsAsync(new Character { Id = CharacterAId, Name = "Arthas", ClassId = 6 });
        _characterRepository.Setup(r => r.GetByIdAsync(CharacterBId, default)).ReturnsAsync(new Character { Id = CharacterBId, Name = "Jaina", ClassId = 8 });
        _characterRepository.Setup(r => r.GetRaidSpecsAsync(CharacterAId, default)).ReturnsAsync(
            [new CharacterRaidSpec { CharacterId = CharacterAId, SpecId = 1, Spec = new Spec { Id = 1, Name = "Blood" } }]);
        _characterRepository.Setup(r => r.GetRaidSpecsAsync(CharacterBId, default)).ReturnsAsync(
            [new CharacterRaidSpec { CharacterId = CharacterBId, SpecId = 2, Spec = new Spec { Id = 2, Name = "Frost" } }]);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _raidCompositionNotifier.Verify(n => n.NotifySlotsSwappedAsync(
            publishedEvent, RequesterId,
            It.Is<RaidCharacterRef>(c => c.Name == "Arthas" && c.ClassId == 6 && c.SpecName == "Blood"), new SlotCoordinate(1, 1),
            It.Is<RaidCharacterRef>(c => c.Name == "Jaina" && c.ClassId == 8 && c.SpecName == "Frost"), new SlotCoordinate(2, 2),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PublishedEventCharactersNoLongerExistAndSpecNoLongerDeclared_FallsBackToCharacterIdAndNullSpec()
    {
        SetupOfficer();
        var publishedEvent = MakeEvent(status: RaidPublicationStatus.Published, assignments: MakeOccupants());
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(publishedEvent);
        _compositionRepository.Setup(r => r.SwapAssignmentsAsync(EventId, 1, 1, 2, 2, default)).ReturnsAsync(true);
        _characterRepository.Setup(r => r.GetByIdAsync(CharacterAId, default)).ReturnsAsync((Character?)null);
        _characterRepository.Setup(r => r.GetByIdAsync(CharacterBId, default)).ReturnsAsync((Character?)null);
        _characterRepository.Setup(r => r.GetRaidSpecsAsync(CharacterAId, default)).ReturnsAsync([]);
        _characterRepository.Setup(r => r.GetRaidSpecsAsync(CharacterBId, default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _raidCompositionNotifier.Verify(n => n.NotifySlotsSwappedAsync(
            publishedEvent, RequesterId,
            It.Is<RaidCharacterRef>(c => c.Name == CharacterAId.ToString() && c.ClassId == null && c.SpecName == null), new SlotCoordinate(1, 1),
            It.Is<RaidCharacterRef>(c => c.Name == CharacterBId.ToString() && c.ClassId == null && c.SpecName == null), new SlotCoordinate(2, 2),
            default), Times.Once);
    }
}
