using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Zones.Queries;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Zones.QueryHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Raids;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Zones.QueryHandlers;

/// <summary>
/// Unit tests for <see cref="GetRaidZonesForGuildQueryHandler"/>.
/// </summary>
public class GetRaidZonesForGuildQueryHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IGuildBranchesRepository> _guildBranches = new();
    private readonly Mock<IBranchRepository> _branches = new();
    private readonly Mock<IRaidZoneRepository> _raidZones = new();
    private readonly GetRaidZonesForGuildQueryHandler _sut;

    private const string GuildId = "guild-1";
    private const string RequesterId = "officer-1";

    private static readonly GetRaidZonesForGuildQuery Query = new() { GuildId = GuildId, RequesterDiscordId = RequesterId };

    public GetRaidZonesForGuildQueryHandlerTests()
    {
        _sut = new GetRaidZonesForGuildQueryHandler(_access.Object, _guildBranches.Object, _branches.Object, _raidZones.Object);
    }

    private void SetupOfficer() => _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);

    [Fact]
    public async Task HandleAsync_NotOfficer_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_TwoBranchesOnSameExpansion_DeduplicatesZones()
    {
        SetupOfficer();
        _guildBranches.Setup(b => b.GetActiveForGuildAsync(GuildId, default)).ReturnsAsync(
        [
            new GuildBranch { Id = 1, BranchId = 100 },
            new GuildBranch { Id = 2, BranchId = 200 },
        ]);
        _branches.Setup(b => b.GetByIdAsync(100, default)).ReturnsAsync(new Branch { Id = 100, CurrentExpansionId = 2 });
        _branches.Setup(b => b.GetByIdAsync(200, default)).ReturnsAsync(new Branch { Id = 200, CurrentExpansionId = 2 });
        _raidZones.Setup(z => z.GetByExpansionIdAsync(2, default)).ReturnsAsync(
        [
            new RaidZone { Id = 4, Name = "Serpentshrine Cavern", ShortCode = "SSC", SortOrder = 1 },
        ]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(z => z.Id == 4 && z.ShortCode == "SSC");
        _raidZones.Verify(z => z.GetByExpansionIdAsync(2, default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TwoBranchesOnDifferentExpansions_ReturnsUnionOfZonesOrderedBySortOrder()
    {
        SetupOfficer();
        _guildBranches.Setup(b => b.GetActiveForGuildAsync(GuildId, default)).ReturnsAsync(
        [
            new GuildBranch { Id = 1, BranchId = 100 },
            new GuildBranch { Id = 2, BranchId = 200 },
        ]);
        _branches.Setup(b => b.GetByIdAsync(100, default)).ReturnsAsync(new Branch { Id = 100, CurrentExpansionId = 1 });
        _branches.Setup(b => b.GetByIdAsync(200, default)).ReturnsAsync(new Branch { Id = 200, CurrentExpansionId = 2 });
        _raidZones.Setup(z => z.GetByExpansionIdAsync(1, default)).ReturnsAsync([new RaidZone { Id = 1, Name = "Molten Core", ShortCode = "MC", SortOrder = 2 }]);
        _raidZones.Setup(z => z.GetByExpansionIdAsync(2, default)).ReturnsAsync([new RaidZone { Id = 4, Name = "Serpentshrine Cavern", ShortCode = "SSC", SortOrder = 1 }]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Select(z => z.Id).Should().ContainInOrder(4, 1);
    }

    [Fact]
    public async Task HandleAsync_BranchNotFound_IsSkipped()
    {
        SetupOfficer();
        _guildBranches.Setup(b => b.GetActiveForGuildAsync(GuildId, default)).ReturnsAsync([new GuildBranch { Id = 1, BranchId = 100 }]);
        _branches.Setup(b => b.GetByIdAsync(100, default)).ReturnsAsync((Branch?)null);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
        _raidZones.Verify(z => z.GetByExpansionIdAsync(It.IsAny<int>(), default), Times.Never);
    }
}
