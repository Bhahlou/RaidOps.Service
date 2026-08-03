using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Assignments.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Assignments.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids;
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

    private static RaidEvent MakeEvent(int groupCount = 3, int slotsPerGroup = 3) => new()
    {
        Id = EventId,
        GroupCount = groupCount,
        SlotsPerGroup = slotsPerGroup,
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
}
