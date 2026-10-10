using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Bosses.Queries;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Bosses.QueryHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Bosses.QueryHandlers;

/// <summary>
/// Unit tests for <see cref="GetRaidBossesForEventQueryHandler"/>.
/// </summary>
public class GetRaidBossesForEventQueryHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IRaidEventRepository> _raidEvents = new();
    private readonly Mock<IRaidBossRepository> _raidBosses = new();
    private readonly GetRaidBossesForEventQueryHandler _sut;

    private const string GuildId = "guild-1";
    private const string RequesterId = "player-1";
    private const int GuildBranchId = 7;
    private const int EventId = 42;

    private static readonly GetRaidBossesForEventQuery Query = new() { GuildId = GuildId, RequesterDiscordId = RequesterId, GuildBranchId = GuildBranchId, EventId = EventId };

    public GetRaidBossesForEventQueryHandlerTests()
    {
        _sut = new GetRaidBossesForEventQueryHandler(_access.Object, _raidEvents.Object, _raidBosses.Object);
    }

    [Fact]
    public async Task HandleAsync_BelowRoster_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Public);

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_RaidEventNotFound_ReturnsRaidEventNotFound()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _raidEvents.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync((RaidEvent?)null);

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidEventNotFound);
    }

    [Fact]
    public async Task HandleAsync_RosterMemberOnUnpublishedDraft_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _raidEvents.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent
        {
            Id = EventId, PublicationStatus = RaidPublicationStatus.Draft, SignupMode = SignupMode.DefaultPresent, TargetZones = [],
        });

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_OfficerOnUnpublishedDraft_Succeeds()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _raidEvents.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent
        {
            Id = EventId, PublicationStatus = RaidPublicationStatus.Draft, SignupMode = SignupMode.DefaultPresent, TargetZones = [],
        });
        _raidBosses.Setup(b => b.GetForZonesAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_Success_ReturnsBossesOfTheEventsTargetZones()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _raidEvents.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent
        {
            Id = EventId,
            PublicationStatus = RaidPublicationStatus.Published,
            TargetZones = [new RaidEventZone { RaidZoneId = 4 }, new RaidEventZone { RaidZoneId = 5 }],
        });
        var zone = new RaidZone { Id = 4, Name = "Serpentshrine Cavern", ShortCode = "SSC" };
        int[] expectedZoneIds = [4, 5];
        _raidBosses.Setup(b => b.GetForZonesAsync(It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(expectedZoneIds)), default)).ReturnsAsync(
        [
            new RaidBoss { Id = 14, RaidZoneId = 4, Name = "Hydross the Unstable", SortOrder = 0, RaidZone = zone },
        ]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(b => b.Id == 14 && b.Name == "Hydross the Unstable");
    }
}
