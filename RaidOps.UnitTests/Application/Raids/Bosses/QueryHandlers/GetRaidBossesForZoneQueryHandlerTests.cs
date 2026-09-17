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
/// Unit tests for <see cref="GetRaidBossesForZoneQueryHandler"/>.
/// </summary>
public class GetRaidBossesForZoneQueryHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IRaidBossRepository> _raidBosses = new();
    private readonly GetRaidBossesForZoneQueryHandler _sut;

    private const string GuildId = "guild-1";
    private const string RequesterId = "officer-1";

    private static readonly GetRaidBossesForZoneQuery Query = new() { GuildId = GuildId, RequesterDiscordId = RequesterId, RaidZoneId = 4 };

    public GetRaidBossesForZoneQueryHandlerTests()
    {
        _sut = new GetRaidBossesForZoneQueryHandler(_access.Object, _raidBosses.Object);
    }

    [Fact]
    public async Task HandleAsync_NotOfficer_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
        _raidBosses.Verify(b => b.GetForZonesAsync(It.IsAny<IEnumerable<int>>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Officer_ReturnsMappedBossesForThatZoneOnly()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        var zone = new RaidZone { Id = 4, Name = "Serpentshrine Cavern", ShortCode = "SSC" };
        _raidBosses.Setup(b => b.GetForZonesAsync(It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 4 })), default)).ReturnsAsync(
        [
            new RaidBoss { Id = 14, RaidZoneId = 4, Name = "Hydross the Unstable", IconUrl = "/hydross.png", SortOrder = 0, RaidZone = zone },
        ]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        var boss = result.Value.Should().ContainSingle().Subject;
        boss.Id.Should().Be(14);
        boss.Name.Should().Be("Hydross the Unstable");
        boss.IconUrl.Should().Be("/hydross.png");
        boss.RaidZoneId.Should().Be(4);
        boss.RaidZoneName.Should().Be("Serpentshrine Cavern");
        boss.RaidZoneShortCode.Should().Be("SSC");
    }
}
