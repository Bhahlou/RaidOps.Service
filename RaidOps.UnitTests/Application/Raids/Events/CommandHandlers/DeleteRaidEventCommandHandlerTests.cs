using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Events.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Events.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Events.CommandHandlers;

public class DeleteRaidEventCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IRaidEventRepository> _raidEventRepository = new();
    private readonly Mock<IGuildsRepository> _guildsRepository = new();
    private readonly Mock<IAuditLogService> _auditLogService = new();
    private readonly Mock<IGuildNotificationDispatcher> _guildNotificationDispatcher = new();
    private readonly Mock<IRaidNotificationContentBuilder> _raidNotificationContentBuilder = new();
    private readonly DeleteRaidEventCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "officer-1";
    private const int EventId = 5;

    public DeleteRaidEventCommandHandlerTests()
    {
        _sut = new DeleteRaidEventCommandHandler(
            _access.Object, _raidEventRepository.Object, _guildsRepository.Object, _auditLogService.Object,
            _guildNotificationDispatcher.Object, _raidNotificationContentBuilder.Object);
    }

    private static DeleteRaidEventCommand MakeCommand() => new()
    {
        GuildId = GuildId,
        RequesterDiscordId = RequesterId,
        GuildBranchId = GuildBranchId,
        EventId = EventId,
    };

    [Fact]
    public async Task HandleAsync_NotOfficer_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
        _raidEventRepository.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_EventNotFound_ReturnsRaidEventNotFound()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync((RaidEvent?)null);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidEventNotFound);
        _raidEventRepository.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Success_DeletesAndLogsAudit()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent { Id = EventId, Name = "Split 1" });

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _raidEventRepository.Verify(r => r.DeleteAsync(EventId, GuildBranchId, default), Times.Once);
        _auditLogService.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.RaidEventDeleted,
            It.Is<Dictionary<string, string>>(d => d["eventName"] == "Split 1"), default), Times.Once);
    }
}
