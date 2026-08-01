using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Assignments.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Assignments.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Assignments.CommandHandlers;

public class UpdateSlotAssignmentSpecCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IRaidEventRepository> _raidEventRepository = new();
    private readonly Mock<ICharacterRepository> _characterRepository = new();
    private readonly Mock<IRaidCompositionRepository> _compositionRepository = new();
    private readonly UpdateSlotAssignmentSpecCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "officer-1";
    private const int EventId = 5;
    private const int CharacterId = 42;

    public UpdateSlotAssignmentSpecCommandHandlerTests()
    {
        _sut = new UpdateSlotAssignmentSpecCommandHandler(_access.Object, _raidEventRepository.Object, _characterRepository.Object, _compositionRepository.Object);
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

    private static RaidEvent MakeEventWithAssignment(int groupNumber = 1, int slotNumber = 2) => new()
    {
        Id = EventId,
        Assignments = [new RaidSlotAssignment { GroupNumber = groupNumber, SlotNumber = slotNumber, CharacterId = CharacterId }],
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
}
