using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Implementations.Raids.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;
using RaidOps.Application.Contracts.Services;

namespace RaidOps.UnitTests.Application.Raids.Services;

public class RaidGridAndZoneValidatorTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IRaidZoneRepository> _raidZoneRepository = new();
    private readonly RaidGridAndZoneValidator _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "officer-1";

    public RaidGridAndZoneValidatorTests()
    {
        _sut = new RaidGridAndZoneValidator(_access.Object, _raidZoneRepository.Object);
    }

    private void SetupOfficer() =>
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);

    [Fact]
    public async Task ValidateAsync_NotOfficer_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.ValidateAsync(RequesterId, GuildId, GuildBranchId, 2, 5, [1]);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(-1, 5)]
    [InlineData(2, 0)]
    [InlineData(2, -1)]
    public async Task ValidateAsync_NonPositiveGridShape_ReturnsInvalidRequest(int groupCount, int slotsPerGroup)
    {
        SetupOfficer();

        var result = await _sut.ValidateAsync(RequesterId, GuildId, GuildBranchId, groupCount, slotsPerGroup, [1]);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidRequest);
    }

    [Fact]
    public async Task ValidateAsync_NoZonesTargeted_ReturnsInvalidRequest()
    {
        SetupOfficer();

        var result = await _sut.ValidateAsync(RequesterId, GuildId, GuildBranchId, 2, 5, []);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidRequest);
        _raidZoneRepository.Verify(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default), Times.Never);
    }

    [Fact]
    public async Task ValidateAsync_UnknownZone_ReturnsRaidZoneNotFound()
    {
        SetupOfficer();
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([]);

        var result = await _sut.ValidateAsync(RequesterId, GuildId, GuildBranchId, 2, 5, [1, 2]);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidZoneNotFound);
    }

    [Fact]
    public async Task ValidateAsync_DuplicateZoneIds_AreDeduplicatedBeforeLookup()
    {
        SetupOfficer();
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.Is<IEnumerable<int>>(ids => ids.Count() == 1 && ids.Contains(1)), default))
            .ReturnsAsync([new RaidZone { Id = 1 }]);

        var result = await _sut.ValidateAsync(RequesterId, GuildId, GuildBranchId, 2, 5, [1, 1]);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Equal(1);
    }

    [Fact]
    public async Task ValidateAsync_Success_ReturnsDistinctZoneIds()
    {
        SetupOfficer();
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([new RaidZone { Id = 1 }, new RaidZone { Id = 2 }]);

        var result = await _sut.ValidateAsync(RequesterId, GuildId, GuildBranchId, 2, 5, [1, 2]);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo([1, 2]);
    }
}
