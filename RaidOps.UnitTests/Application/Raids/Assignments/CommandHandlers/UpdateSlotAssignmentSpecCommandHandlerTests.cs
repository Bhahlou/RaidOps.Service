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

public class UpdateSlotAssignmentSpecCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IRaidEventRepository> _raidEventRepository = new();
    private readonly Mock<ICharacterRepository> _characterRepository = new();
    private readonly Mock<IRaidCompositionRepository> _compositionRepository = new();
    private readonly Mock<IRaidCompositionNotifier> _raidCompositionNotifier = new();
    private readonly UpdateSlotAssignmentSpecCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "officer-1";
    private const int EventId = 5;
    private const int CharacterId = 42;
    private const string PlayerDiscordId = "player-1";

    public UpdateSlotAssignmentSpecCommandHandlerTests()
    {
        _sut = new UpdateSlotAssignmentSpecCommandHandler(
            _access.Object, _raidEventRepository.Object, _characterRepository.Object, _compositionRepository.Object,
            _raidCompositionNotifier.Object);
    }

    private static UpdateSlotAssignmentSpecCommand MakeCommand(int specId = 99) => new()
    {
        GuildId = GuildId,
        RequesterDiscordId = RequesterId,
        GuildBranchId = GuildBranchId,
        EventId = EventId,
        GroupNumber = 1,
        SlotNumber = 2,
        SpecId = specId,
    };

    private static RaidEvent MakeEventWithAssignment(int groupNumber = 1, int slotNumber = 2, RaidPublicationStatus status = RaidPublicationStatus.Draft, int oldSpecId = 0) => new()
    {
        Id = EventId,
        PublicationStatus = status,
        Assignments = [new RaidSlotAssignment { GroupNumber = groupNumber, SlotNumber = slotNumber, CharacterId = CharacterId, SpecId = oldSpecId, AssignedPlayerDiscordId = PlayerDiscordId }],
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

    [Fact]
    public async Task HandleAsync_SlotEmpty_ReturnsSlotEmpty()
    {
        SetupOfficer();
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent { Id = EventId, Assignments = [] });

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.SlotEmpty);
    }

    [Fact]
    public async Task HandleAsync_OtherSlotsOccupiedButTargetCoordinateEmpty_ReturnsSlotEmpty()
    {
        // Distinct from the fully-empty-list case above: an assignment exists in the event, just
        // not at the requested (GroupNumber, SlotNumber) coordinate. Same GroupNumber as requested
        // (1) so the match fails on the SlotNumber half of the predicate (1 vs 2).
        SetupOfficer();
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent
        {
            Id = EventId,
            Assignments = [new RaidSlotAssignment { GroupNumber = 1, SlotNumber = 1, CharacterId = CharacterId }],
        });

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.SlotEmpty);
    }

    [Fact]
    public async Task HandleAsync_AssignmentExistsInDifferentGroup_ReturnsSlotEmpty()
    {
        // Different GroupNumber than requested (2 vs 1) — the predicate short-circuits on its
        // first half, distinct from the SlotNumber-mismatch case above.
        SetupOfficer();
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent
        {
            Id = EventId,
            Assignments = [new RaidSlotAssignment { GroupNumber = 2, SlotNumber = 2, CharacterId = CharacterId }],
        });

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.SlotEmpty);
    }

    [Fact]
    public async Task HandleAsync_SpecNotDeclaredByCharacter_ReturnsSpecNotAvailableForCharacter()
    {
        SetupOfficer();
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(MakeEventWithAssignment());
        _characterRepository.Setup(r => r.GetRaidSpecsAsync(CharacterId, default))
            .ReturnsAsync([new CharacterRaidSpec { CharacterId = CharacterId, SpecId = 1 }]);

        var result = await _sut.HandleAsync(MakeCommand(specId: 99));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.SpecNotAvailableForCharacter);
        _compositionRepository.Verify(r => r.UpdateAssignmentSpecAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Success_UpdatesSpecAndReturnsOk()
    {
        SetupOfficer();
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(MakeEventWithAssignment());
        _characterRepository.Setup(r => r.GetRaidSpecsAsync(CharacterId, default))
            .ReturnsAsync([new CharacterRaidSpec { CharacterId = CharacterId, SpecId = 99 }]);

        var result = await _sut.HandleAsync(MakeCommand(specId: 99));

        result.IsSuccess.Should().BeTrue();
        _compositionRepository.Verify(r => r.UpdateAssignmentSpecAsync(EventId, 1, 2, 99, default), Times.Once);
    }

    // ── Draft vs Published notification gating ──────────────────────────────

    [Fact]
    public async Task HandleAsync_DraftEvent_Succeeds_ButDoesNotNotify()
    {
        SetupOfficer();
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(MakeEventWithAssignment(oldSpecId: 1));
        _characterRepository.Setup(r => r.GetRaidSpecsAsync(CharacterId, default)).ReturnsAsync(
        [
            new CharacterRaidSpec { CharacterId = CharacterId, SpecId = 1, Spec = new Spec { Id = 1, Name = "Arms" } },
            new CharacterRaidSpec { CharacterId = CharacterId, SpecId = 99, Spec = new Spec { Id = 99, Name = "Fury" } },
        ]);

        var result = await _sut.HandleAsync(MakeCommand(specId: 99));

        result.IsSuccess.Should().BeTrue();
        _raidCompositionNotifier.Verify(n => n.NotifySlotSpecChangedAsync(
            It.IsAny<RaidEvent>(), It.IsAny<string>(), It.IsAny<RaidCharacterRef>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PublishedEvent_NotifiesWithOldAndNewSpecNames()
    {
        SetupOfficer();
        var publishedEvent = MakeEventWithAssignment(status: RaidPublicationStatus.Published, oldSpecId: 1);
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(publishedEvent);
        _characterRepository.Setup(r => r.GetRaidSpecsAsync(CharacterId, default)).ReturnsAsync(
        [
            new CharacterRaidSpec { CharacterId = CharacterId, SpecId = 1, Spec = new Spec { Id = 1, Name = "Arms" } },
            new CharacterRaidSpec { CharacterId = CharacterId, SpecId = 99, Spec = new Spec { Id = 99, Name = "Fury" } },
        ]);
        _characterRepository.Setup(r => r.GetByIdAsync(CharacterId, default)).ReturnsAsync(new Character { Id = CharacterId, Name = "Bhahlouslam", ClassId = 1 });

        var result = await _sut.HandleAsync(MakeCommand(specId: 99));

        result.IsSuccess.Should().BeTrue();
        _raidCompositionNotifier.Verify(n => n.NotifySlotSpecChangedAsync(
            publishedEvent, RequesterId,
            It.Is<RaidCharacterRef>(c => c.Name == "Bhahlouslam" && c.ClassId == 1 && c.SpecName == null),
            PlayerDiscordId, "Arms", "Fury",
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PublishedEventOldSpecNoLongerDeclared_FallsBackToRawSpecIdForOldSpecName()
    {
        SetupOfficer();
        // Character no longer declares spec 1 as raid-viable — only the new spec (99) is present.
        var publishedEvent = MakeEventWithAssignment(status: RaidPublicationStatus.Published, oldSpecId: 1);
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(publishedEvent);
        _characterRepository.Setup(r => r.GetRaidSpecsAsync(CharacterId, default)).ReturnsAsync(
        [
            new CharacterRaidSpec { CharacterId = CharacterId, SpecId = 99, Spec = new Spec { Id = 99, Name = "Fury" } },
        ]);
        _characterRepository.Setup(r => r.GetByIdAsync(CharacterId, default)).ReturnsAsync(new Character { Id = CharacterId, Name = "Bhahlouslam", ClassId = 1 });

        var result = await _sut.HandleAsync(MakeCommand(specId: 99));

        result.IsSuccess.Should().BeTrue();
        _raidCompositionNotifier.Verify(n => n.NotifySlotSpecChangedAsync(
            publishedEvent, RequesterId, It.IsAny<RaidCharacterRef>(), PlayerDiscordId, "1", "Fury", default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PublishedEventCharacterNoLongerExists_FallsBackToCharacterIdAsName()
    {
        SetupOfficer();
        var publishedEvent = MakeEventWithAssignment(status: RaidPublicationStatus.Published, oldSpecId: 1);
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(publishedEvent);
        _characterRepository.Setup(r => r.GetRaidSpecsAsync(CharacterId, default)).ReturnsAsync(
        [
            new CharacterRaidSpec { CharacterId = CharacterId, SpecId = 1, Spec = new Spec { Id = 1, Name = "Arms" } },
            new CharacterRaidSpec { CharacterId = CharacterId, SpecId = 99, Spec = new Spec { Id = 99, Name = "Fury" } },
        ]);
        _characterRepository.Setup(r => r.GetByIdAsync(CharacterId, default)).ReturnsAsync((Character?)null);

        var result = await _sut.HandleAsync(MakeCommand(specId: 99));

        result.IsSuccess.Should().BeTrue();
        _raidCompositionNotifier.Verify(n => n.NotifySlotSpecChangedAsync(
            publishedEvent, RequesterId,
            It.Is<RaidCharacterRef>(c => c.Name == CharacterId.ToString() && c.ClassId == null),
            PlayerDiscordId, "Arms", "Fury", default), Times.Once);
    }
}
