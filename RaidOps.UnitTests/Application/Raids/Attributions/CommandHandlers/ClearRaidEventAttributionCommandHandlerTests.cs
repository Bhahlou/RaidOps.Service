using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Attributions.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Attributions.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Attributions.CommandHandlers;

/// <summary>
/// Unit tests for <see cref="ClearRaidEventAttributionCommandHandler"/>.
/// </summary>
public class ClearRaidEventAttributionCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IRaidEventAttributionsRepository> _attributions = new();
    private readonly Mock<IAuditLogService> _auditLog = new();
    private readonly ClearRaidEventAttributionCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const string RequesterId = "officer-1";
    private const int GuildBranchId = 7;
    private const int EventId = 42;
    private const int DefinitionId = 1;
    private const int CellId = 2;

    private static readonly ClearRaidEventAttributionCommand Command = new()
    {
        GuildId = GuildId, RequesterDiscordId = RequesterId, GuildBranchId = GuildBranchId, EventId = EventId,
        DefinitionId = DefinitionId, CellId = CellId, InstanceIndex = 3,
    };

    public ClearRaidEventAttributionCommandHandlerTests()
    {
        _sut = new ClearRaidEventAttributionCommandHandler(_access.Object, _attributions.Object, _auditLog.Object);
    }

    [Fact]
    public async Task HandleAsync_NotOfficer_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
        _attributions.Verify(a => a.ClearAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AlreadyEmpty_ReturnsSlotEmpty()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _attributions.Setup(a => a.ClearAsync(EventId, CellId, 3, default)).ReturnsAsync(false);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.SlotEmpty);
        _auditLog.Verify(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GuildAuditAction>(), It.IsAny<Dictionary<string, string>>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Success_ClearsSlotAndLogs()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _attributions.Setup(a => a.ClearAsync(EventId, CellId, 3, default)).ReturnsAsync(true);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.RaidEventAttributionUpdated,
            It.Is<Dictionary<string, string>>(v => v["definitionId"] == DefinitionId.ToString()),
            default), Times.Once);
    }
}
