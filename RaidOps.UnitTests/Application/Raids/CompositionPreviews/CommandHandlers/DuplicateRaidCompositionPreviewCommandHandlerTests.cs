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

public class DuplicateRaidCompositionPreviewCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IRaidCompositionPreviewsRepository> _previews = new();
    private readonly Mock<IAuditLogService> _auditLogService = new();
    private readonly DuplicateRaidCompositionPreviewCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "officer-1";
    private const int PreviewId = 5;

    public DuplicateRaidCompositionPreviewCommandHandlerTests()
    {
        _sut = new DuplicateRaidCompositionPreviewCommandHandler(_access.Object, _previews.Object, _auditLogService.Object);
    }

    private static DuplicateRaidCompositionPreviewCommand MakeCommand() => new()
    {
        GuildId = GuildId,
        GuildBranchId = GuildBranchId,
        RequesterDiscordId = RequesterId,
        PreviewId = PreviewId,
        NewName = "Copy",
    };

    [Fact]
    public async Task HandleAsync_NotOfficer_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
        _previews.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SourceNotFound_ReturnsRaidCompositionPreviewNotFound()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _previews.Setup(r => r.GetByIdAsync(PreviewId, GuildBranchId, default)).ReturnsAsync((RaidCompositionPreview?)null);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidCompositionPreviewNotFound);
        _previews.Verify(r => r.AddAsync(It.IsAny<RaidCompositionPreview>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Success_ClonesGridShapeAndSlots()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        var source = new RaidCompositionPreview
        {
            Id = PreviewId,
            Name = "Original",
            GroupCount = 4,
            SlotsPerGroup = 5,
            Slots =
            [
                new RaidCompositionPreviewSlot { GroupNumber = 1, SlotNumber = 1, WowClassId = 1, SpecId = 71, Note = "Bob" },
            ],
        };
        _previews.Setup(r => r.GetByIdAsync(PreviewId, GuildBranchId, default)).ReturnsAsync(source);
        RaidCompositionPreview? added = null;
        _previews.Setup(r => r.AddAsync(It.IsAny<RaidCompositionPreview>(), default))
            .Callback<RaidCompositionPreview, CancellationToken>((p, _) => added = p)
            .ReturnsAsync((RaidCompositionPreview p, CancellationToken _) => { p.Id = 99; return p; });

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        added.Should().NotBeNull();
        added!.Name.Should().Be("Copy");
        added.GuildBranchId.Should().Be(GuildBranchId);
        added.GroupCount.Should().Be(4);
        added.SlotsPerGroup.Should().Be(5);
        added.CreatedByDiscordId.Should().Be(RequesterId);
        added.Slots.Should().ContainSingle(s => s.GroupNumber == 1 && s.SlotNumber == 1 && s.WowClassId == 1 && s.SpecId == 71 && s.Note == "Bob");
    }

    [Fact]
    public async Task HandleAsync_Success_LogsAuditWithBothNames()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        var source = new RaidCompositionPreview { Id = PreviewId, Name = "Original", GroupCount = 4, SlotsPerGroup = 5 };
        _previews.Setup(r => r.GetByIdAsync(PreviewId, GuildBranchId, default)).ReturnsAsync(source);
        _previews.Setup(r => r.AddAsync(It.IsAny<RaidCompositionPreview>(), default))
            .ReturnsAsync((RaidCompositionPreview p, CancellationToken _) => p);

        await _sut.HandleAsync(MakeCommand());

        _auditLogService.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.RaidCompositionPreviewUpdated,
            It.Is<Dictionary<string, string>>(d => d["previewName"] == "Copy" && d["duplicatedFrom"] == "Original"), default), Times.Once);
    }
}
