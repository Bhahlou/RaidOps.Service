using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Signups.Queries;
using RaidOps.Application.Contracts.Raids.Signups.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Signups.QueryHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Signups.QueryHandlers;

public class GetRaidSignupsQueryHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IRaidEventRepository> _raidEventRepository = new();
    private readonly Mock<IRaidSignupResponseBuilder> _raidSignupResponseBuilder = new();
    private readonly GetRaidSignupsQueryHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "officer-1";
    private const int EventId = 5;

    private static readonly GetRaidSignupsQuery Query = new() { GuildId = GuildId, RequesterDiscordId = RequesterId, GuildBranchId = GuildBranchId, EventId = EventId };
    private static readonly RaidEvent Event = new() { Id = EventId, GuildBranchId = GuildBranchId };

    public GetRaidSignupsQueryHandlerTests()
    {
        _sut = new GetRaidSignupsQueryHandler(_access.Object, _raidEventRepository.Object, _raidSignupResponseBuilder.Object);
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(Event);
        _raidSignupResponseBuilder.Setup(b => b.BuildAsync(Event, default)).ReturnsAsync([]);
    }

    private static RaidSignupResponse MakeResponse(string userDiscordId, string? playerName = null) => new() { UserDiscordId = userDiscordId, PlayerName = playerName };

    [Fact]
    public async Task HandleAsync_BelowRosterAccess_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Public);

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
        _raidEventRepository.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_RosterAccess_Succeeds()
    {
        // Deliberately Roster, not Officer — GetRaidSignupsQuery was relaxed to any roster member.
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_EventNotFound_ReturnsRaidEventNotFound()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync((RaidEvent?)null);

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidEventNotFound);
        _raidSignupResponseBuilder.Verify(b => b.BuildAsync(It.IsAny<RaidEvent>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_DelegatesToTheResponseBuilderForTheFetchedEvent()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        _raidSignupResponseBuilder.Verify(b => b.BuildAsync(Event, default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ReturnsTheBuilderResponsesSortedByPlayerNameCaseInsensitive()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _raidSignupResponseBuilder.Setup(b => b.BuildAsync(Event, default)).ReturnsAsync(
        [
            MakeResponse("player-a", "zeta"),
            MakeResponse("player-b", "Alpha"),
        ]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Select(r => r.PlayerName).Should().Equal("Alpha", "zeta");
    }
}
