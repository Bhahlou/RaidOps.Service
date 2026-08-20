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

public class UpdateGuildBranchSignupModeCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IGuildBranchesRepository> _guildBranchesRepository = new();
    private readonly Mock<IBranchRepository> _branchRepository = new();
    private readonly Mock<IAuditLogService> _auditLogService = new();
    private readonly Mock<ILogger<UpdateGuildBranchSignupModeCommandHandler>> _logger = new();
    private readonly UpdateGuildBranchSignupModeCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "officer-1";
    private const int BranchId = 3;

    public UpdateGuildBranchSignupModeCommandHandlerTests()
    {
        _logger.Setup(l => l.IsEnabled(LogLevel.Information)).Returns(true);
        _sut = new UpdateGuildBranchSignupModeCommandHandler(_access.Object, _guildBranchesRepository.Object, _branchRepository.Object, _auditLogService.Object, _logger.Object);
    }

    private static UpdateGuildBranchSignupModeCommand MakeCommand(SignupMode signupMode = SignupMode.Signup) => new()
    {
        GuildId = GuildId,
        RequesterDiscordId = RequesterId,
        GuildBranchId = GuildBranchId,
        SignupMode = signupMode,
    };

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
        _guildBranchesRepository.Verify(r => r.UpdateSignupModeAsync(It.IsAny<int>(), It.IsAny<SignupMode>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SignupModeUnchanged_ReturnsOkWithoutUpdating()
    {
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId, BranchId = BranchId, SignupMode = SignupMode.Signup });
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);

        var result = await _sut.HandleAsync(MakeCommand(signupMode: SignupMode.Signup));

        result.IsSuccess.Should().BeTrue();
        _guildBranchesRepository.Verify(r => r.UpdateSignupModeAsync(It.IsAny<int>(), It.IsAny<SignupMode>(), default), Times.Never);
        _auditLogService.Verify(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GuildAuditAction>(), It.IsAny<Dictionary<string, string>>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Success_UpdatesSignupModeAndLogsWithPreviousMode()
    {
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId, BranchId = BranchId, SignupMode = SignupMode.DefaultPresent });
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _branchRepository.Setup(r => r.GetByIdAsync(BranchId, default)).ReturnsAsync(new Branch { Id = BranchId, Name = "Classic Era" });

        var result = await _sut.HandleAsync(MakeCommand(signupMode: SignupMode.Signup));

        result.IsSuccess.Should().BeTrue();
        _guildBranchesRepository.Verify(r => r.UpdateSignupModeAsync(GuildBranchId, SignupMode.Signup, default), Times.Once);
        _auditLogService.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.BranchSignupModeUpdated,
            It.Is<Dictionary<string, string>>(v =>
                v["branchId"] == BranchId.ToString() && v["branchName"] == "Classic Era" &&
                v["oldSignupMode"] == "DefaultPresent" && v["newSignupMode"] == "Signup"),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PreviouslyUnconfiguredSignupMode_LogsNoneAsOldMode()
    {
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId, BranchId = BranchId, SignupMode = null });
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _branchRepository.Setup(r => r.GetByIdAsync(BranchId, default)).ReturnsAsync(new Branch { Id = BranchId, Name = "Classic Era" });

        var result = await _sut.HandleAsync(MakeCommand(signupMode: SignupMode.Signup));

        result.IsSuccess.Should().BeTrue();
        _auditLogService.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.BranchSignupModeUpdated,
            It.Is<Dictionary<string, string>>(v => v["oldSignupMode"] == "(none)"),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WowBranchNotResolvable_LogsFallbackBranchName()
    {
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId, BranchId = BranchId, SignupMode = SignupMode.DefaultPresent });
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _branchRepository.Setup(r => r.GetByIdAsync(BranchId, default)).ReturnsAsync((Branch?)null);

        var result = await _sut.HandleAsync(MakeCommand(signupMode: SignupMode.Signup));

        result.IsSuccess.Should().BeTrue();
        _auditLogService.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.BranchSignupModeUpdated,
            It.Is<Dictionary<string, string>>(v => v["branchName"] == "Unknown"),
            default), Times.Once);
    }
}
