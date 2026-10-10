using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Raids;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Services;

public class RaidSlotUnassignmentServiceTests
{
    private readonly Mock<ICharacterRepository> _characterRepository = new();
    private readonly Mock<IRaidCompositionRepository> _compositionRepository = new();
    private readonly Mock<IRaidCompositionNotifier> _raidCompositionNotifier = new();
    private readonly RaidSlotUnassignmentService _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "officer-1";
    private const int EventId = 5;
    private const string PlayerDiscordId = "player-1";
    private const int CharacterId = 42;

    public RaidSlotUnassignmentServiceTests()
    {
        _sut = new RaidSlotUnassignmentService(_characterRepository.Object, _compositionRepository.Object, _raidCompositionNotifier.Object);
    }

    private static RaidEvent MakeEvent(RaidPublicationStatus status = RaidPublicationStatus.Draft, List<RaidSlotAssignment>? assignments = null) => new()
    {
        Id = EventId,
        GuildId = GuildId,
        GuildBranchId = GuildBranchId,
        Name = "Split 1",
        PublicationStatus = status,
        Assignments = assignments ?? [],
    };

    [Fact]
    public async Task UnassignAsync_SlotAlreadyEmpty_ReturnsFalseAndDoesNotNotify()
    {
        _compositionRepository.Setup(r => r.UnassignAsync(EventId, 1, 2, default)).ReturnsAsync(false);

        var result = await _sut.UnassignAsync(MakeEvent(), 1, 2, RequesterId);

        result.Should().BeFalse();
        _raidCompositionNotifier.Verify(n => n.NotifySlotUnassignedAsync(
            It.IsAny<RaidEvent>(), It.IsAny<string>(), It.IsAny<RaidCharacterRef>(), It.IsAny<string>(), It.IsAny<SlotCoordinate>(), default), Times.Never);
    }

    [Fact]
    public async Task UnassignAsync_DraftEventWithOccupant_ReturnsTrue_ButDoesNotNotify()
    {
        var raidEvent = MakeEvent(assignments:
        [
            new RaidSlotAssignment { GroupNumber = 1, SlotNumber = 2, CharacterId = CharacterId, SpecId = 1 },
        ]);
        _compositionRepository.Setup(r => r.UnassignAsync(EventId, 1, 2, default)).ReturnsAsync(true);

        var result = await _sut.UnassignAsync(raidEvent, 1, 2, RequesterId);

        result.Should().BeTrue();
        _raidCompositionNotifier.Verify(n => n.NotifySlotUnassignedAsync(
            It.IsAny<RaidEvent>(), It.IsAny<string>(), It.IsAny<RaidCharacterRef>(), It.IsAny<string>(), It.IsAny<SlotCoordinate>(), default), Times.Never);
    }

    [Fact]
    public async Task UnassignAsync_PublishedEventWithOccupant_NotifiesWithResolvedCharacterClassAndSpec()
    {
        var publishedEvent = MakeEvent(status: RaidPublicationStatus.Published, assignments:
        [
            new RaidSlotAssignment { GroupNumber = 1, SlotNumber = 2, CharacterId = CharacterId, SpecId = 1, AssignedPlayerDiscordId = PlayerDiscordId },
        ]);
        _compositionRepository.Setup(r => r.UnassignAsync(EventId, 1, 2, default)).ReturnsAsync(true);
        _characterRepository.Setup(r => r.GetByIdAsync(CharacterId, default)).ReturnsAsync(new Character { Id = CharacterId, Name = "Arthas", ClassId = 6 });
        _characterRepository.Setup(r => r.GetRaidSpecsAsync(CharacterId, default)).ReturnsAsync(
        [
            new CharacterRaidSpec { CharacterId = CharacterId, SpecId = 1, Spec = new Spec { Id = 1, Name = "Blood" } },
        ]);

        var result = await _sut.UnassignAsync(publishedEvent, 1, 2, RequesterId);

        result.Should().BeTrue();
        _raidCompositionNotifier.Verify(n => n.NotifySlotUnassignedAsync(
            publishedEvent, RequesterId,
            It.Is<RaidCharacterRef>(c => c.Name == "Arthas" && c.ClassId == 6 && c.SpecName == "Blood"),
            PlayerDiscordId,
            new SlotCoordinate(1, 2),
            default), Times.Once);
    }

    [Fact]
    public async Task UnassignAsync_PublishedEventAssignmentExistsAtDifferentCoordinate_TreatsOccupantAsNullAndDoesNotNotify()
    {
        // Assignment shares the requested GroupNumber (1) but sits at a different SlotNumber (5) —
        // exercises the SlotNumber half of the predicate short-circuiting to false after the
        // GroupNumber half already matched. A repository race (DB says occupied, in-memory
        // snapshot disagrees) still must not crash or notify with garbage data.
        var raidEvent = MakeEvent(status: RaidPublicationStatus.Published, assignments:
        [
            new RaidSlotAssignment { GroupNumber = 1, SlotNumber = 5, CharacterId = CharacterId, SpecId = 1 },
        ]);
        _compositionRepository.Setup(r => r.UnassignAsync(EventId, 1, 2, default)).ReturnsAsync(true);

        var result = await _sut.UnassignAsync(raidEvent, 1, 2, RequesterId);

        result.Should().BeTrue();
        _raidCompositionNotifier.Verify(n => n.NotifySlotUnassignedAsync(
            It.IsAny<RaidEvent>(), It.IsAny<string>(), It.IsAny<RaidCharacterRef>(), It.IsAny<string>(), It.IsAny<SlotCoordinate>(), default), Times.Never);
    }

    [Fact]
    public async Task UnassignAsync_PublishedEventAssignmentExistsInDifferentGroup_TreatsOccupantAsNullAndDoesNotNotify()
    {
        // Different GroupNumber (99 vs 1) with a matching SlotNumber (2) — the predicate
        // short-circuits to false on the GroupNumber half, distinct from the SlotNumber-mismatch
        // case above where GroupNumber already matched.
        var raidEvent = MakeEvent(status: RaidPublicationStatus.Published, assignments:
        [
            new RaidSlotAssignment { GroupNumber = 99, SlotNumber = 2, CharacterId = CharacterId, SpecId = 1 },
        ]);
        _compositionRepository.Setup(r => r.UnassignAsync(EventId, 1, 2, default)).ReturnsAsync(true);

        var result = await _sut.UnassignAsync(raidEvent, 1, 2, RequesterId);

        result.Should().BeTrue();
        _raidCompositionNotifier.Verify(n => n.NotifySlotUnassignedAsync(
            It.IsAny<RaidEvent>(), It.IsAny<string>(), It.IsAny<RaidCharacterRef>(), It.IsAny<string>(), It.IsAny<SlotCoordinate>(), default), Times.Never);
    }

    [Fact]
    public async Task UnassignAsync_PublishedEventOccupantCharacterNoLongerExists_FallsBackToCharacterIdAsName()
    {
        var publishedEvent = MakeEvent(status: RaidPublicationStatus.Published, assignments:
        [
            new RaidSlotAssignment { GroupNumber = 1, SlotNumber = 2, CharacterId = CharacterId, SpecId = 1, AssignedPlayerDiscordId = PlayerDiscordId },
        ]);
        _compositionRepository.Setup(r => r.UnassignAsync(EventId, 1, 2, default)).ReturnsAsync(true);
        _characterRepository.Setup(r => r.GetByIdAsync(CharacterId, default)).ReturnsAsync((Character?)null);
        _characterRepository.Setup(r => r.GetRaidSpecsAsync(CharacterId, default)).ReturnsAsync([]);

        var result = await _sut.UnassignAsync(publishedEvent, 1, 2, RequesterId);

        result.Should().BeTrue();
        _raidCompositionNotifier.Verify(n => n.NotifySlotUnassignedAsync(
            publishedEvent, RequesterId,
            It.Is<RaidCharacterRef>(c => c.Name == CharacterId.ToString() && c.ClassId == null && c.SpecName == null),
            PlayerDiscordId,
            new SlotCoordinate(1, 2),
            default), Times.Once);
    }
}
