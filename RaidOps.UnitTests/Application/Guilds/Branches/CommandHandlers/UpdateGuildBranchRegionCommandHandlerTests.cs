using FluentAssertions;
using Microsoft.Extensions.Logging;
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

public class UpdateGuildBranchRegionCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IGuildBranchesRepository> _guildBranchesRepository = new();
    private readonly Mock<IBranchRepository> _branchRepository = new();
    private readonly Mock<IAuditLogService> _auditLogService = new();
    private readonly Mock<ILogger<UpdateGuildBranchRegionCommandHandler>> _logger = new();
    private readonly UpdateGuildBranchRegionCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "officer-1";
    private const int BranchId = 3;

    public UpdateGuildBranchRegionCommandHandlerTests()
    {
        _logger.Setup(l => l.IsEnabled(LogLevel.Information)).Returns(true);
        _sut = new UpdateGuildBranchRegionCommandHandler(_access.Object, _guildBranchesRepository.Object, _branchRepository.Object, _auditLogService.Object, _logger.Object);
    }

    private static UpdateGuildBranchRegionCommand MakeCommand(string region = "eu") => new()
    {
        GuildId = GuildId,
        RequesterDiscordId = RequesterId,
        GuildBranchId = GuildBranchId,
        Region = region,
    };

    [Fact]
    public async Task HandleAsync_UnrecognizedRegion_ReturnsInvalidRegion()
    {
        var result = await _sut.HandleAsync(MakeCommand(region: "atlantis"));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidRegion);
        _guildBranchesRepository.Verify(r => r.GetByIdAsync(It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_BranchNotFound_ReturnsGuildBranchNotFound()
    {
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync((GuildBranch?)null);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildBranchNotFound);
    }

    [Fact]
    public async Task HandleAsync_BranchBelongsToDifferentGuild_ReturnsGuildBranchNotFound()
    {
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = "other-guild", BranchId = BranchId });

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildBranchNotFound);
    }

    [Fact]
    public async Task HandleAsync_NotOfficer_ReturnsForbidden()
    {
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId, BranchId = BranchId });
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
        _guildBranchesRepository.Verify(r => r.UpdateRegionAsync(It.IsAny<int>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_RegionUnchanged_ReturnsOkWithoutUpdating()
    {
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId, BranchId = BranchId, Region = "eu" });
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);

        var result = await _sut.HandleAsync(MakeCommand(region: "eu"));

        result.IsSuccess.Should().BeTrue();
        _guildBranchesRepository.Verify(r => r.UpdateRegionAsync(It.IsAny<int>(), It.IsAny<string>(), default), Times.Never);
        _auditLogService.Verify(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GuildAuditAction>(), It.IsAny<Dictionary<string, string>>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Success_UpdatesRegionAndLogsWithPreviousRegion()
    {
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId, BranchId = BranchId, Region = "us" });
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _branchRepository.Setup(r => r.GetByIdAsync(BranchId, default)).ReturnsAsync(new Branch { Id = BranchId, Name = "Classic Era" });

        var result = await _sut.HandleAsync(MakeCommand(region: "eu"));

        result.IsSuccess.Should().BeTrue();
        _guildBranchesRepository.Verify(r => r.UpdateRegionAsync(GuildBranchId, "eu", default), Times.Once);
        _auditLogService.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.BranchRegionUpdated,
            It.Is<Dictionary<string, string>>(v =>
                v["branchId"] == BranchId.ToString() && v["branchName"] == "Classic Era" &&
                v["oldRegion"] == "us" && v["newRegion"] == "eu"),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PreviouslyUnconfiguredRegion_LogsNoneAsOldRegion()
    {
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId, BranchId = BranchId, Region = null });
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _branchRepository.Setup(r => r.GetByIdAsync(BranchId, default)).ReturnsAsync(new Branch { Id = BranchId, Name = "Classic Era" });

        var result = await _sut.HandleAsync(MakeCommand(region: "eu"));

        result.IsSuccess.Should().BeTrue();
        _auditLogService.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.BranchRegionUpdated,
            It.Is<Dictionary<string, string>>(v => v["oldRegion"] == "(none)"),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WowBranchNotResolvable_LogsFallbackBranchName()
    {
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId, BranchId = BranchId, Region = "us" });
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _branchRepository.Setup(r => r.GetByIdAsync(BranchId, default)).ReturnsAsync((Branch?)null);

        var result = await _sut.HandleAsync(MakeCommand(region: "eu"));

        result.IsSuccess.Should().BeTrue();
        _auditLogService.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.BranchRegionUpdated,
            It.Is<Dictionary<string, string>>(v => v["branchName"] == "Unknown"),
            default), Times.Once);
    }
}
