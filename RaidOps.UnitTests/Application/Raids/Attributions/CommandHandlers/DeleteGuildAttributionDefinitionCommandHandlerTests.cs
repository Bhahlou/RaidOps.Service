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
/// Unit tests for <see cref="DeleteGuildAttributionDefinitionCommandHandler"/>.
/// </summary>
public class DeleteGuildAttributionDefinitionCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IGuildAttributionDefinitionsRepository> _definitions = new();
    private readonly Mock<IAuditLogService> _auditLog = new();
    private readonly DeleteGuildAttributionDefinitionCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const string RequesterId = "officer-1";
    private const int DefinitionId = 7;

    private static readonly DeleteGuildAttributionDefinitionCommand Command = new()
    {
        GuildId = GuildId, RequesterDiscordId = RequesterId, DefinitionId = DefinitionId,
    };

    public DeleteGuildAttributionDefinitionCommandHandlerTests()
    {
        _sut = new DeleteGuildAttributionDefinitionCommandHandler(_access.Object, _definitions.Object, _auditLog.Object);
    }

    [Fact]
    public async Task HandleAsync_NotOfficer_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
        _definitions.Verify(d => d.DeleteAsync(It.IsAny<int>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_NotFound_ReturnsAttributionDefinitionNotFound()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _definitions.Setup(d => d.DeleteAsync(DefinitionId, GuildId, default)).ReturnsAsync(false);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.AttributionDefinitionNotFound);
    }

    [Fact]
    public async Task HandleAsync_Success_DeletesAndLogs()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _definitions.Setup(d => d.DeleteAsync(DefinitionId, GuildId, default)).ReturnsAsync(true);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.AttributionTemplateUpdated,
            It.Is<Dictionary<string, string>>(v => v["definitionId"] == DefinitionId.ToString()),
            default), Times.Once);
    }
}
