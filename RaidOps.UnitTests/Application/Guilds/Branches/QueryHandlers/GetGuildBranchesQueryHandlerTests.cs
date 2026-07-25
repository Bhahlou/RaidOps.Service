using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Guilds.Branches.Queries;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Guilds.Branches.QueryHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Guilds.Branches.QueryHandlers;

/// <summary>
/// Unit tests for <see cref="GetGuildBranchesQueryHandler"/>.
/// </summary>
public class GetGuildBranchesQueryHandlerTests
{
    private readonly Mock<IGuildsRepository>        _guilds        = new();
    private readonly Mock<IGuildBranchesRepository> _guildBranches = new();
    private readonly Mock<IBranchRepository>        _branchRepo    = new();
    private readonly Mock<IGuildAccessService>      _access        = new();
    private readonly GetGuildBranchesQueryHandler   _sut;

    private const string GuildId     = "guild-1";
    private const string RequesterId = "user-1";

    private static readonly GetGuildBranchesQuery Query = new() { GuildId = GuildId, RequesterDiscordId = RequesterId };

    public GetGuildBranchesQueryHandlerTests()
    {
        _sut = new GetGuildBranchesQueryHandler(_guilds.Object, _guildBranches.Object, _branchRepo.Object, _access.Object);
    }

    [Fact]
    public async Task HandleAsync_GuildNotFound_ReturnsGuildNotFound()
    {
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync((Guild?)null);

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildNotFound);
    }

    [Fact]
    public async Task HandleAsync_RequesterNotAdmin_ReturnsForbidden()
    {
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true });
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_Success_MapsBranchesWithResolvedNames()
    {
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true });
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guildBranches.Setup(b => b.GetAllForGuildAsync(GuildId, default)).ReturnsAsync(
        [
            new GuildBranch
            {
                Id = 1, GuildId = GuildId, BranchId = 2, IsActive = true,
                RosterMode = RosterMode.DiscordRoleOnly, RosterRoleIds = ["r1"], OfficerRoleIds = ["r2"],
            },
        ]);
        _branchRepo.Setup(b => b.GetAllAsync(default)).ReturnsAsync(
        [
            new Branch { Id = 2, Name = "Classic Era", BnetNamespacePrefix = "dynamic-classic1x", CurrentExpansionId = 1 },
        ]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        var branch = result.Value!.Single();
        branch.Id.Should().Be(1);
        branch.BranchId.Should().Be(2);
        branch.BranchName.Should().Be("Classic Era");
        branch.IsActive.Should().BeTrue();
        branch.RosterMode.Should().Be(RosterMode.DiscordRoleOnly);
        branch.RosterRoleIds.Should().Equal("r1");
        branch.OfficerRoleIds.Should().Equal("r2");
    }

    [Fact]
    public async Task HandleAsync_UnknownWowBranch_MapsBranchNameAsUnknown()
    {
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true });
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guildBranches.Setup(b => b.GetAllForGuildAsync(GuildId, default)).ReturnsAsync(
        [
            new GuildBranch { Id = 1, GuildId = GuildId, BranchId = 999 },
        ]);
        _branchRepo.Setup(b => b.GetAllAsync(default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(Query, default);

        result.Value!.Single().BranchName.Should().Be("Unknown");
    }
}
