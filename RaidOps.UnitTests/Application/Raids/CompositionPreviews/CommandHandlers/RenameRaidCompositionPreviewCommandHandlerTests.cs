using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.CompositionPreviews.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.CompositionPreviews.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.CompositionPreviews.CommandHandlers;

public class RenameRaidCompositionPreviewCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IRaidCompositionPreviewsRepository> _previews = new();
    private readonly RenameRaidCompositionPreviewCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "officer-1";
    private const int PreviewId = 5;

    public RenameRaidCompositionPreviewCommandHandlerTests()
    {
        _sut = new RenameRaidCompositionPreviewCommandHandler(_access.Object, _previews.Object);
    }

    private static RenameRaidCompositionPreviewCommand MakeCommand() => new()
    {
        GuildId = GuildId,
        GuildBranchId = GuildBranchId,
        RequesterDiscordId = RequesterId,
        PreviewId = PreviewId,
        Name = "New name",
    };

    [Fact]
    public async Task HandleAsync_NotOfficer_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
        _previews.Verify(r => r.RenameAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_NotFound_ReturnsRaidCompositionPreviewNotFound()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _previews.Setup(r => r.RenameAsync(PreviewId, GuildBranchId, "New name", default)).ReturnsAsync(false);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidCompositionPreviewNotFound);
    }

    [Fact]
    public async Task HandleAsync_Success_ReturnsOk()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _previews.Setup(r => r.RenameAsync(PreviewId, GuildBranchId, "New name", default)).ReturnsAsync(true);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
    }
}
