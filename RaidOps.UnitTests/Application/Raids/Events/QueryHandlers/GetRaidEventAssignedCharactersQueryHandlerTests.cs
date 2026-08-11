using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Events.Queries;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Events.QueryHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Events.QueryHandlers;

public class GetRaidEventAssignedCharactersQueryHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IRaidEventRepository> _raidEventRepository = new();
    private readonly Mock<IRaidCompositionRepository> _raidCompositionRepository = new();
    private readonly GetRaidEventAssignedCharactersQueryHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "officer-1";
    private const int EventId = 5;

    public GetRaidEventAssignedCharactersQueryHandlerTests()
    {
        _sut = new GetRaidEventAssignedCharactersQueryHandler(_access.Object, _raidEventRepository.Object, _raidCompositionRepository.Object);
    }

    private static GetRaidEventAssignedCharactersQuery MakeQuery() => new()
    {
        GuildId = GuildId,
        GuildBranchId = GuildBranchId,
        EventId = EventId,
        RequesterDiscordId = RequesterId,
    };

    [Fact]
    public async Task HandleAsync_NotOfficer_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
        _raidEventRepository.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_EventNotFound_ReturnsRaidEventNotFound()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync((RaidEvent?)null);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidEventNotFound);
    }

    [Fact]
    public async Task HandleAsync_Success_ReturnsCharactersOrderedByNameCaseInsensitive()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent { Id = EventId, Name = "Split 1" });
        _raidCompositionRepository.Setup(r => r.GetAssignmentsForEventAsync(EventId, default)).ReturnsAsync(
        [
            new RaidSlotAssignment { CharacterId = 2, Character = new Character { Id = 2, Name = "zul" } },
            new RaidSlotAssignment { CharacterId = 1, Character = new Character { Id = 1, Name = "Arthas" } },
        ]);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().SatisfyRespectively(
            c => { c.CharacterId.Should().Be(1); c.Name.Should().Be("Arthas"); },
            c => { c.CharacterId.Should().Be(2); c.Name.Should().Be("zul"); });
    }

    [Fact]
    public async Task HandleAsync_NoAssignments_ReturnsEmptyList()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent { Id = EventId, Name = "Split 1" });
        _raidCompositionRepository.Setup(r => r.GetAssignmentsForEventAsync(EventId, default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
