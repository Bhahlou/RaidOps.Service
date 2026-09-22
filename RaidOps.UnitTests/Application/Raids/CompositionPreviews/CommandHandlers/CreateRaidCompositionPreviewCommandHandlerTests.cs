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

public class CreateRaidCompositionPreviewCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IRaidCompositionPreviewsRepository> _previews = new();
    private readonly Mock<IAuditLogService> _auditLogService = new();
    private readonly CreateRaidCompositionPreviewCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "officer-1";

    public CreateRaidCompositionPreviewCommandHandlerTests()
    {
        _sut = new CreateRaidCompositionPreviewCommandHandler(_access.Object, _previews.Object, _auditLogService.Object);
    }

    private static CreateRaidCompositionPreviewCommand MakeCommand(int groupCount = 8) => new()
    {
        GuildId = GuildId,
        GuildBranchId = GuildBranchId,
        RequesterDiscordId = RequesterId,
        Name = "40-man target",
        GroupCount = groupCount,
    };

    [Fact]
    public async Task HandleAsync_NotOfficer_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
        _previews.Verify(r => r.AddAsync(It.IsAny<RaidCompositionPreview>(), default), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(9)]
    public async Task HandleAsync_GroupCountOutOfRange_ReturnsInvalidGroupCount(int groupCount)
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);

        var result = await _sut.HandleAsync(MakeCommand(groupCount));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidGroupCount);
        _previews.Verify(r => r.AddAsync(It.IsAny<RaidCompositionPreview>(), default), Times.Never);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    public async Task HandleAsync_GroupCountAtBounds_Succeeds(int groupCount)
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _previews.Setup(r => r.AddAsync(It.IsAny<RaidCompositionPreview>(), default))
            .ReturnsAsync((RaidCompositionPreview p, CancellationToken _) => p);

        var result = await _sut.HandleAsync(MakeCommand(groupCount));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_Success_CreatesPreviewWithFiveSlotsPerGroupAndReturnsId()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        RaidCompositionPreview? added = null;
        _previews.Setup(r => r.AddAsync(It.IsAny<RaidCompositionPreview>(), default))
            .Callback<RaidCompositionPreview, CancellationToken>((p, _) => added = p)
            .ReturnsAsync((RaidCompositionPreview p, CancellationToken _) => { p.Id = 42; return p; });

        var result = await _sut.HandleAsync(MakeCommand(8));

        result.IsSuccess.Should().BeTrue();
        added.Should().NotBeNull();
        added!.GuildBranchId.Should().Be(GuildBranchId);
        added.Name.Should().Be("40-man target");
        added.GroupCount.Should().Be(8);
        added.SlotsPerGroup.Should().Be(5);
        added.CreatedByDiscordId.Should().Be(RequesterId);
    }

    [Fact]
    public async Task HandleAsync_Success_LogsAudit()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _previews.Setup(r => r.AddAsync(It.IsAny<RaidCompositionPreview>(), default))
            .ReturnsAsync((RaidCompositionPreview p, CancellationToken _) => p);

        await _sut.HandleAsync(MakeCommand());

        _auditLogService.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.RaidCompositionPreviewUpdated,
            It.Is<Dictionary<string, string>>(d => d["previewName"] == "40-man target"), default), Times.Once);
    }
}
