using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.CompositionPreviews.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.CompositionPreviews.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids.CompositionPreviews;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.CompositionPreviews.CommandHandlers;

public class UpdateRaidCompositionPreviewSlotCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IRaidCompositionPreviewsRepository> _previews = new();
    private readonly Mock<IWowClassRepository> _wowClasses = new();
    private readonly Mock<ISpecRepository> _specs = new();
    private readonly UpdateRaidCompositionPreviewSlotCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "officer-1";
    private const int PreviewId = 5;

    private static readonly RaidCompositionPreview Preview = new() { Id = PreviewId, GroupCount = 4, SlotsPerGroup = 5 };

    public UpdateRaidCompositionPreviewSlotCommandHandlerTests()
    {
        _sut = new UpdateRaidCompositionPreviewSlotCommandHandler(_access.Object, _previews.Object, _wowClasses.Object, _specs.Object);
        _previews.Setup(r => r.GetByIdAsync(PreviewId, GuildBranchId, default)).ReturnsAsync(Preview);
    }

    private static UpdateRaidCompositionPreviewSlotCommand MakeCommand(
        int groupNumber = 1, int slotNumber = 1, int? wowClassId = null, int? specId = null, string? note = null) => new()
    {
        GuildId = GuildId,
        GuildBranchId = GuildBranchId,
        RequesterDiscordId = RequesterId,
        PreviewId = PreviewId,
        GroupNumber = groupNumber,
        SlotNumber = slotNumber,
        WowClassId = wowClassId,
        SpecId = specId,
        Note = note,
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
    public async Task HandleAsync_PreviewNotFound_ReturnsRaidCompositionPreviewNotFound()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _previews.Setup(r => r.GetByIdAsync(999, GuildBranchId, default)).ReturnsAsync((RaidCompositionPreview?)null);
        var command = MakeCommand();
        command.PreviewId = 999;

        var result = await _sut.HandleAsync(command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidCompositionPreviewNotFound);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(5, 1)] // GroupCount is 4
    [InlineData(1, 0)]
    [InlineData(1, 6)] // SlotsPerGroup is 5
    public async Task HandleAsync_GroupOrSlotNumberOutOfBounds_ReturnsInvalidGroupOrSlotNumber(int groupNumber, int slotNumber)
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);

        var result = await _sut.HandleAsync(MakeCommand(groupNumber, slotNumber));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidGroupOrSlotNumber);
        _previews.Verify(r => r.UpsertSlotAsync(It.IsAny<RaidCompositionPreviewSlot>(), default), Times.Never);
        _previews.Verify(r => r.ClearSlotAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SpecIdNotFound_ReturnsInvalidRequest()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _specs.Setup(r => r.GetAllAsync(default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(MakeCommand(specId: 999));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidRequest);
    }

    [Fact]
    public async Task HandleAsync_SpecIdGivenWithoutClassId_DerivesClassFromSpecAndUpserts()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _specs.Setup(r => r.GetAllAsync(default)).ReturnsAsync([new Spec { Id = 71, Name = "Arms", ClassId = 1 }]);
        RaidCompositionPreviewSlot? upserted = null;
        _previews.Setup(r => r.UpsertSlotAsync(It.IsAny<RaidCompositionPreviewSlot>(), default))
            .Callback<RaidCompositionPreviewSlot, CancellationToken>((s, _) => upserted = s);

        var result = await _sut.HandleAsync(MakeCommand(specId: 71));

        result.IsSuccess.Should().BeTrue();
        upserted.Should().NotBeNull();
        upserted!.WowClassId.Should().Be(1);
        upserted.SpecId.Should().Be(71);
    }

    [Fact]
    public async Task HandleAsync_SpecIdAndMatchingClassId_Upserts()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _specs.Setup(r => r.GetAllAsync(default)).ReturnsAsync([new Spec { Id = 71, Name = "Arms", ClassId = 1 }]);

        var result = await _sut.HandleAsync(MakeCommand(wowClassId: 1, specId: 71));

        result.IsSuccess.Should().BeTrue();
        _previews.Verify(r => r.UpsertSlotAsync(It.Is<RaidCompositionPreviewSlot>(s => s.WowClassId == 1 && s.SpecId == 71), default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_SpecIdAndMismatchedClassId_ReturnsSpecClassMismatch()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _specs.Setup(r => r.GetAllAsync(default)).ReturnsAsync([new Spec { Id = 71, Name = "Arms", ClassId = 1 }]);

        var result = await _sut.HandleAsync(MakeCommand(wowClassId: 2, specId: 71));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.SpecClassMismatch);
        _previews.Verify(r => r.UpsertSlotAsync(It.IsAny<RaidCompositionPreviewSlot>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ClassIdOnly_NotFound_ReturnsInvalidRequest()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _wowClasses.Setup(r => r.GetAllAsync(default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(MakeCommand(wowClassId: 999));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidRequest);
    }

    [Fact]
    public async Task HandleAsync_ClassIdOnly_Found_Upserts()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _wowClasses.Setup(r => r.GetAllAsync(default)).ReturnsAsync([new WowClass { Id = 1, Name = "Warrior", Color = "C79C6E" }]);

        var result = await _sut.HandleAsync(MakeCommand(wowClassId: 1));

        result.IsSuccess.Should().BeTrue();
        _previews.Verify(r => r.UpsertSlotAsync(It.Is<RaidCompositionPreviewSlot>(s => s.WowClassId == 1 && s.SpecId == null), default), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_NoClassSpecAndBlankNote_ClearsSlot(string? note)
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);

        var result = await _sut.HandleAsync(MakeCommand(note: note));

        result.IsSuccess.Should().BeTrue();
        _previews.Verify(r => r.ClearSlotAsync(PreviewId, 1, 1, default), Times.Once);
        _previews.Verify(r => r.UpsertSlotAsync(It.IsAny<RaidCompositionPreviewSlot>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_NoteOnlyProvided_Upserts()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);

        var result = await _sut.HandleAsync(MakeCommand(note: "Bob"));

        result.IsSuccess.Should().BeTrue();
        _previews.Verify(r => r.UpsertSlotAsync(
            It.Is<RaidCompositionPreviewSlot>(s => s.WowClassId == null && s.SpecId == null && s.Note == "Bob"), default), Times.Once);
        _previews.Verify(r => r.ClearSlotAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhitespaceNoteWithClassSet_UpsertsWithNullNote()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _wowClasses.Setup(r => r.GetAllAsync(default)).ReturnsAsync([new WowClass { Id = 1, Name = "Warrior", Color = "C79C6E" }]);

        var result = await _sut.HandleAsync(MakeCommand(wowClassId: 1, note: "   "));

        result.IsSuccess.Should().BeTrue();
        _previews.Verify(r => r.UpsertSlotAsync(It.Is<RaidCompositionPreviewSlot>(s => s.WowClassId == 1 && s.Note == null), default), Times.Once);
    }
}
