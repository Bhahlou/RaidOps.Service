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
/// Unit tests for <see cref="DeactivateGuildBranchCommandHandler"/>.
/// </summary>
public class DeactivateGuildBranchCommandHandlerTests
{
    private readonly Mock<IGuildAccessService>      _access        = new();
    private readonly Mock<IGuildBranchesRepository> _guildBranches = new();
    private readonly Mock<IBranchRepository>        _branchRepo    = new();
    private readonly Mock<IAuditLogService>         _auditLog      = new();
    private readonly DeactivateGuildBranchCommandHandler _sut;

    private const string GuildId       = "guild-1";
    private const string RequesterId   = "user-1";
    private const int    GuildBranchId = 1;
    private const int    BranchId      = 3;

    private static readonly DeactivateGuildBranchCommand Command = new()
    {
        GuildId = GuildId, RequesterDiscordId = RequesterId, GuildBranchId = GuildBranchId,
    };

    public DeactivateGuildBranchCommandHandlerTests()
    {
        _branchRepo.Setup(b => b.GetByIdAsync(BranchId, default)).ReturnsAsync(new Branch { Id = BranchId, Name = "Classic Era" });
        _sut = new DeactivateGuildBranchCommandHandler(_access.Object, _guildBranches.Object, _branchRepo.Object, _auditLog.Object, NullLogger<DeactivateGuildBranchCommandHandler>.Instance);
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
    public async Task HandleAsync_BranchNotFound_ReturnsGuildBranchNotFound()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guildBranches.Setup(b => b.GetByIdAsync(GuildBranchId, default)).ReturnsAsync((GuildBranch?)null);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildBranchNotFound);
    }

    [Fact]
    public async Task HandleAsync_BranchBelongsToDifferentGuild_ReturnsGuildBranchNotFound()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guildBranches.Setup(b => b.GetByIdAsync(GuildBranchId, default))
            .ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = "other-guild", BranchId = BranchId });

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildBranchNotFound);
    }

    [Fact]
    public async Task HandleAsync_Success_DeactivatesAndLogs()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guildBranches.Setup(b => b.GetByIdAsync(GuildBranchId, default))
            .ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId, BranchId = BranchId });

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _guildBranches.Verify(b => b.DeactivateAsync(GuildBranchId, default), Times.Once);
        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.BranchDeactivated,
            It.Is<Dictionary<string, string>>(v => v["branchId"] == BranchId.ToString() && v["branchName"] == "Classic Era"),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WowBranchNotResolvable_LogsFallbackBranchName()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guildBranches.Setup(b => b.GetByIdAsync(GuildBranchId, default))
            .ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId, BranchId = BranchId });
        _branchRepo.Setup(b => b.GetByIdAsync(BranchId, default)).ReturnsAsync((Branch?)null);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.BranchDeactivated,
            It.Is<Dictionary<string, string>>(v => v["branchName"] == "Unknown"),
            default), Times.Once);
    }
}
