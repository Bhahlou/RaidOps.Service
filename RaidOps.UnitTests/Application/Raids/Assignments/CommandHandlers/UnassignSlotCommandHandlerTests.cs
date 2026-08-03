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

    private static RaidEvent MakeEvent(RaidPublicationStatus status = RaidPublicationStatus.Draft) => new()
    {
        Id = EventId,
        GuildId = GuildId,
        GuildBranchId = GuildBranchId,
        Name = "Split 1",
        PublicationStatus = status,
        Assignments = [],
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
}
