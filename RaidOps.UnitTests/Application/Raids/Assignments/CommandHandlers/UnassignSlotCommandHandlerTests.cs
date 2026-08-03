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

public class UnassignSlotCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IRaidEventRepository> _raidEventRepository = new();
    private readonly Mock<ICharacterRepository> _characterRepository = new();
    private readonly Mock<IRaidCompositionRepository> _compositionRepository = new();
    private readonly Mock<IRaidCompositionNotifier> _raidCompositionNotifier = new();
    private readonly UnassignSlotCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "officer-1";
    private const int EventId = 5;

    public UnassignSlotCommandHandlerTests()
    {
        _sut = new UnassignSlotCommandHandler(
            _access.Object, _raidEventRepository.Object, _characterRepository.Object, _compositionRepository.Object,
            _raidCompositionNotifier.Object);

        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(MakeEvent());
    }

    private static UnassignSlotCommand MakeCommand() => new()
    {
        GuildId = GuildId,
        RequesterDiscordId = RequesterId,
        GuildBranchId = GuildBranchId,
        EventId = EventId,
        GroupNumber = 1,
        SlotNumber = 2,
    };

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
    public async Task HandleAsync_NotOfficer_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
        _compositionRepository.Verify(r => r.UnassignAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SlotAlreadyEmpty_ReturnsNotFound()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _compositionRepository.Setup(r => r.UnassignAsync(EventId, 1, 2, default)).ReturnsAsync(false);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.NotFound);
    }

    [Fact]
    public async Task HandleAsync_Success_ClearsSlotAndReturnsOk()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _compositionRepository.Setup(r => r.UnassignAsync(EventId, 1, 2, default)).ReturnsAsync(true);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _compositionRepository.Verify(r => r.UnassignAsync(EventId, 1, 2, default), Times.Once);
    }

    // ── Draft vs Published notification gating ──────────────────────────────

    private const int CharacterId = 42;

    [Fact]
    public async Task HandleAsync_DraftEventWithOccupant_Succeeds_ButDoesNotNotify()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(MakeEvent(assignments:
        [
            new RaidSlotAssignment { GroupNumber = 1, SlotNumber = 2, CharacterId = CharacterId, SpecId = 1 },
        ]));
        _compositionRepository.Setup(r => r.UnassignAsync(EventId, 1, 2, default)).ReturnsAsync(true);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _raidCompositionNotifier.Verify(n => n.NotifySlotUnassignedAsync(
            It.IsAny<RaidEvent>(), It.IsAny<string>(), It.IsAny<RaidCharacterRef>(), It.IsAny<SlotCoordinate>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PublishedEventWithOccupant_NotifiesWithResolvedCharacterClassAndSpec()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        var publishedEvent = MakeEvent(status: RaidPublicationStatus.Published, assignments:
        [
            new RaidSlotAssignment { GroupNumber = 1, SlotNumber = 2, CharacterId = CharacterId, SpecId = 1 },
        ]);
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(publishedEvent);
        _compositionRepository.Setup(r => r.UnassignAsync(EventId, 1, 2, default)).ReturnsAsync(true);
        _characterRepository.Setup(r => r.GetByIdAsync(CharacterId, default)).ReturnsAsync(new Character { Id = CharacterId, Name = "Arthas", ClassId = 6 });
        _characterRepository.Setup(r => r.GetRaidSpecsAsync(CharacterId, default)).ReturnsAsync(
        [
            new CharacterRaidSpec { CharacterId = CharacterId, SpecId = 1, Spec = new Spec { Id = 1, Name = "Blood" } },
        ]);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _raidCompositionNotifier.Verify(n => n.NotifySlotUnassignedAsync(
            publishedEvent, RequesterId,
            It.Is<RaidCharacterRef>(c => c.Name == "Arthas" && c.ClassId == 6 && c.SpecName == "Blood"),
            new SlotCoordinate(1, 2),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PublishedEventOccupantCharacterNoLongerExists_FallsBackToCharacterIdAsName()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        var publishedEvent = MakeEvent(status: RaidPublicationStatus.Published, assignments:
        [
            new RaidSlotAssignment { GroupNumber = 1, SlotNumber = 2, CharacterId = CharacterId, SpecId = 1 },
        ]);
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(publishedEvent);
        _compositionRepository.Setup(r => r.UnassignAsync(EventId, 1, 2, default)).ReturnsAsync(true);
        _characterRepository.Setup(r => r.GetByIdAsync(CharacterId, default)).ReturnsAsync((Character?)null);
        _characterRepository.Setup(r => r.GetRaidSpecsAsync(CharacterId, default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _raidCompositionNotifier.Verify(n => n.NotifySlotUnassignedAsync(
            publishedEvent, RequesterId,
            It.Is<RaidCharacterRef>(c => c.Name == CharacterId.ToString() && c.ClassId == null && c.SpecName == null),
            new SlotCoordinate(1, 2),
            default), Times.Once);
    }
}
