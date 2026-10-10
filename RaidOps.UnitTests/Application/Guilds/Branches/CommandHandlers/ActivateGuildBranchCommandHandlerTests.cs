using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Guilds.Branches.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Guilds.Branches.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Guilds.Branches.CommandHandlers;

/// <summary>
/// Unit tests for <see cref="ActivateGuildBranchCommandHandler"/>.
/// </summary>
public class ActivateGuildBranchCommandHandlerTests
{
    private readonly Mock<IGuildAccessService>      _access        = new();
    private readonly Mock<IGuildsRepository>        _guilds        = new();
    private readonly Mock<IGuildBranchesRepository> _guildBranches = new();
    private readonly Mock<IBranchRepository>        _branchRepo    = new();
    private readonly Mock<IAuditLogService>         _auditLog      = new();
    private readonly ActivateGuildBranchCommandHandler _sut;

    private const string GuildId     = "guild-1";
    private const string RequesterId = "user-1";
    private const int    BranchId    = 3;

    private static readonly ActivateGuildBranchCommand Command = new()
    {
        GuildId = GuildId, RequesterDiscordId = RequesterId, BranchId = BranchId,
    };

    public ActivateGuildBranchCommandHandlerTests()
    {
        _branchRepo.Setup(b => b.GetByIdAsync(BranchId, default)).ReturnsAsync(new Branch { Id = BranchId, Name = "Classic Era" });
        _sut = new ActivateGuildBranchCommandHandler(_access.Object, _guilds.Object, _guildBranches.Object, _branchRepo.Object, _auditLog.Object, NullLogger<ActivateGuildBranchCommandHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_RequesterNotAdmin_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_GuildNotFound_ReturnsGuildNotFound()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync((Guild?)null);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildNotFound);
    }

    [Fact]
    public async Task HandleAsync_GuildNotRegistered_ReturnsGuildNotRegistered()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = false });

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildNotRegistered);
    }

    [Fact]
    public async Task HandleAsync_BranchAlreadyActive_ReturnsGuildBranchAlreadyActive()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true });
        _guildBranches.Setup(b => b.GetByGuildAndBranchAsync(GuildId, BranchId, default))
            .ReturnsAsync(new GuildBranch { Id = 1, GuildId = GuildId, BranchId = BranchId, IsActive = true });

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildBranchAlreadyActive);
        _guildBranches.Verify(b => b.ActivateAsync(It.IsAny<string>(), It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_NewBranch_ActivatesAndLogs()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true });
        _guildBranches.Setup(b => b.GetByGuildAndBranchAsync(GuildId, BranchId, default))
            .ReturnsAsync((GuildBranch?)null);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _guildBranches.Verify(b => b.ActivateAsync(GuildId, BranchId, default), Times.Once);
        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.BranchActivated,
            It.Is<Dictionary<string, string>>(v => v["branchId"] == BranchId.ToString() && v["branchName"] == "Classic Era"),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WowBranchNotResolvable_LogsFallbackBranchName()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true });
        _guildBranches.Setup(b => b.GetByGuildAndBranchAsync(GuildId, BranchId, default))
            .ReturnsAsync((GuildBranch?)null);
        _branchRepo.Setup(b => b.GetByIdAsync(BranchId, default)).ReturnsAsync((Branch?)null);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.BranchActivated,
            It.Is<Dictionary<string, string>>(v => v["branchName"] == "Unknown"),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PreviouslyDeactivatedBranch_Reactivates()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true });
        _guildBranches.Setup(b => b.GetByGuildAndBranchAsync(GuildId, BranchId, default))
            .ReturnsAsync(new GuildBranch { Id = 1, GuildId = GuildId, BranchId = BranchId, IsActive = false });

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _guildBranches.Verify(b => b.ActivateAsync(GuildId, BranchId, default), Times.Once);
    }
}
