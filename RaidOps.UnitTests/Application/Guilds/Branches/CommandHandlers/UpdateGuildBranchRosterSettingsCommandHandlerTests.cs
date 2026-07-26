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
/// Unit tests for <see cref="UpdateGuildBranchRosterSettingsCommandHandler"/>.
/// </summary>
public class UpdateGuildBranchRosterSettingsCommandHandlerTests
{
    private readonly Mock<IGuildAccessService>      _access        = new();
    private readonly Mock<IGuildBranchesRepository> _guildBranches = new();
    private readonly Mock<IBranchRepository>        _branchRepo    = new();
    private readonly Mock<IAuditLogService>         _auditLog      = new();
    private readonly UpdateGuildBranchRosterSettingsCommandHandler _sut;

    private const string GuildId       = "guild-1";
    private const string RequesterId   = "user-1";
    private const int    GuildBranchId = 1;
    private const int    BranchId      = 3;

    public UpdateGuildBranchRosterSettingsCommandHandlerTests()
    {
        _branchRepo.Setup(b => b.GetByIdAsync(BranchId, default)).ReturnsAsync(new Branch { Id = BranchId, Name = "Classic Era" });
        _sut = new UpdateGuildBranchRosterSettingsCommandHandler(_access.Object, _guildBranches.Object, _branchRepo.Object, _auditLog.Object, NullLogger<UpdateGuildBranchRosterSettingsCommandHandler>.Instance);
    }

    private static UpdateGuildBranchRosterSettingsCommand MakeCommand(RosterMode rosterMode = RosterMode.Open, List<string>? rosterRoleIds = null, List<string>? officerRoleIds = null)
        => new()
        {
            GuildId = GuildId, RequesterDiscordId = RequesterId, GuildBranchId = GuildBranchId,
            RosterMode = rosterMode, RosterRoleIds = rosterRoleIds ?? [], OfficerRoleIds = officerRoleIds ?? [],
        };

    private static GuildBranch MakeBranch(RosterMode? rosterMode = null, List<string>? rosterRoleIds = null, List<string>? officerRoleIds = null)
        => new()
        {
            Id = GuildBranchId, GuildId = GuildId, BranchId = BranchId,
            RosterMode = rosterMode, RosterRoleIds = rosterRoleIds ?? [], OfficerRoleIds = officerRoleIds ?? [],
        };

    [Fact]
    public async Task HandleAsync_BranchNotFound_ReturnsGuildBranchNotFound()
    {
        _guildBranches.Setup(b => b.GetByIdAsync(GuildBranchId, default)).ReturnsAsync((GuildBranch?)null);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildBranchNotFound);
    }

    [Fact]
    public async Task HandleAsync_BranchBelongsToDifferentGuild_ReturnsGuildBranchNotFound()
    {
        _guildBranches.Setup(b => b.GetByIdAsync(GuildBranchId, default))
            .ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = "other-guild", BranchId = BranchId });

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildBranchNotFound);
    }

    [Fact]
    public async Task HandleAsync_RequesterNotOfficerOfBranch_ReturnsForbidden()
    {
        _guildBranches.Setup(b => b.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(MakeBranch());
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
        _guildBranches.Verify(b => b.UpdateRosterSettingsAsync(It.IsAny<int>(), It.IsAny<RosterMode>(), It.IsAny<List<string>>(), It.IsAny<List<string>>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Success_UpdatesSettingsAndLogsAllChangedFields()
    {
        _guildBranches.Setup(b => b.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(MakeBranch(rosterMode: RosterMode.Open, rosterRoleIds: [], officerRoleIds: []));
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        var command = MakeCommand(rosterMode: RosterMode.DiscordRoleOnly, rosterRoleIds: ["role-1"], officerRoleIds: ["role-2"]);

        var result = await _sut.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        _guildBranches.Verify(b => b.UpdateRosterSettingsAsync(GuildBranchId, RosterMode.DiscordRoleOnly, command.RosterRoleIds, command.OfficerRoleIds, default), Times.Once);
        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.BranchRosterSettingsUpdated,
            It.Is<Dictionary<string, string>>(v =>
                v["branchId"] == BranchId.ToString() && v["branchName"] == "Classic Era"
                && v["changedFields"] == "rosterMode,rosterRoleIds,officerRoleIds"),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_NothingChanged_DoesNotLog()
    {
        _guildBranches.Setup(b => b.GetByIdAsync(GuildBranchId, default))
            .ReturnsAsync(MakeBranch(rosterMode: RosterMode.Open, rosterRoleIds: [], officerRoleIds: ["role-2"]));
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        var command = MakeCommand(rosterMode: RosterMode.Open, rosterRoleIds: [], officerRoleIds: ["role-2"]);

        var result = await _sut.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        _auditLog.Verify(a => a.LogAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GuildAuditAction>(),
            It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
