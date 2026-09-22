using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.CompositionPreviews.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.CompositionPreviews.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids.CompositionPreviews;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.CompositionPreviews.CommandHandlers;

public class DeleteRaidCompositionPreviewCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IRaidCompositionPreviewsRepository> _previews = new();
    private readonly Mock<IAuditLogService> _auditLogService = new();
    private readonly DeleteRaidCompositionPreviewCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "officer-1";
    private const int PreviewId = 5;

    public DeleteRaidCompositionPreviewCommandHandlerTests()
    {
        _sut = new DeleteRaidCompositionPreviewCommandHandler(_access.Object, _previews.Object, _auditLogService.Object);
    }

    private static DeleteRaidCompositionPreviewCommand MakeCommand() => new()
    {
        GuildId = GuildId,
        GuildBranchId = GuildBranchId,
        RequesterDiscordId = RequesterId,
        PreviewId = PreviewId,
    };

    [Fact]
    public async Task HandleAsync_NotOfficer_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
        _previews.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_NotFound_ReturnsRaidCompositionPreviewNotFound()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _previews.Setup(r => r.GetByIdAsync(PreviewId, GuildBranchId, default)).ReturnsAsync((RaidCompositionPreview?)null);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidCompositionPreviewNotFound);
        _previews.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Success_DeletesAndLogsAudit()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _previews.Setup(r => r.GetByIdAsync(PreviewId, GuildBranchId, default)).ReturnsAsync(new RaidCompositionPreview { Id = PreviewId, Name = "To delete" });

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _previews.Verify(r => r.DeleteAsync(PreviewId, GuildBranchId, default), Times.Once);
        _auditLogService.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.RaidCompositionPreviewUpdated,
            It.Is<Dictionary<string, string>>(d => d["previewName"] == "To delete"), default), Times.Once);
    }
}
