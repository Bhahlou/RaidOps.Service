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

public class GetRaidZonesForBranchQueryHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IGuildBranchesRepository> _guildBranchesRepository = new();
    private readonly Mock<IBranchRepository> _branchRepository = new();
    private readonly Mock<IRaidZoneRepository> _raidZoneRepository = new();
    private readonly GetRaidZonesForBranchQueryHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "roster-1";
    private const int BranchId = 2;
    private const int ExpansionId = 3;

    public GetRaidZonesForBranchQueryHandlerTests()
    {
        _sut = new GetRaidZonesForBranchQueryHandler(_access.Object, _guildBranchesRepository.Object, _branchRepository.Object, _raidZoneRepository.Object);
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);
    }

    private static GetRaidZonesForBranchQuery MakeQuery() => new()
    {
        GuildId = GuildId,
        RequesterDiscordId = RequesterId,
        GuildBranchId = GuildBranchId,
    };

    [Fact]
    public async Task HandleAsync_BelowRosterAccess_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Public);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_GuildBranchNotFound_ReturnsBranchNotFound()
    {
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync((GuildBranch?)null);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.BranchNotFound);
    }

    [Fact]
    public async Task HandleAsync_WowBranchNotFound_ReturnsBranchNotFound()
    {
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId, BranchId = BranchId });
        _branchRepository.Setup(r => r.GetByIdAsync(BranchId, default)).ReturnsAsync((Branch?)null);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.BranchNotFound);
    }

    [Fact]
    public async Task HandleAsync_Success_ReturnsZonesForCurrentExpansion()
    {
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId, BranchId = BranchId });
        _branchRepository.Setup(r => r.GetByIdAsync(BranchId, default)).ReturnsAsync(new Branch { Id = BranchId, CurrentExpansionId = ExpansionId });
        _raidZoneRepository.Setup(r => r.GetByExpansionIdAsync(ExpansionId, default)).ReturnsAsync([
            new RaidZone { Id = 7, Name = "Molten Core", ShortCode = "MC", GroupCount = 8, SlotsPerGroup = 5, SortOrder = 1 },
        ]);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsSuccess.Should().BeTrue();
        var zone = result.Value!.Single();
        zone.Id.Should().Be(7);
        zone.Name.Should().Be("Molten Core");
        zone.ShortCode.Should().Be("MC");
        zone.GroupCount.Should().Be(8);
        zone.SlotsPerGroup.Should().Be(5);
        zone.SortOrder.Should().Be(1);
    }
}
