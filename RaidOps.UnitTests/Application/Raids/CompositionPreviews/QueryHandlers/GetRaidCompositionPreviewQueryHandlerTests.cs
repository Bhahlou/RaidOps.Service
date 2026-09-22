using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.CompositionPreviews.Queries;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.CompositionPreviews.QueryHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids.CompositionPreviews;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.CompositionPreviews.QueryHandlers;

public class GetRaidCompositionPreviewQueryHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IRaidCompositionPreviewsRepository> _previews = new();
    private readonly GetRaidCompositionPreviewQueryHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "officer-1";
    private const int PreviewId = 5;

    public GetRaidCompositionPreviewQueryHandlerTests()
    {
        _sut = new GetRaidCompositionPreviewQueryHandler(_access.Object, _previews.Object);
    }

    private static GetRaidCompositionPreviewQuery MakeQuery() => new()
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

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_NotFound_ReturnsRaidCompositionPreviewNotFound()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _previews.Setup(r => r.GetByIdAsync(PreviewId, GuildBranchId, default)).ReturnsAsync((RaidCompositionPreview?)null);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidCompositionPreviewNotFound);
    }

    [Fact]
    public async Task HandleAsync_Success_MapsPreviewAndFilledSlotWithClassColorPrefixed()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        var preview = new RaidCompositionPreview
        {
            Id = PreviewId,
            Name = "40-man",
            GroupCount = 8,
            SlotsPerGroup = 5,
            Slots =
            [
                new RaidCompositionPreviewSlot
                {
                    GroupNumber = 1,
                    SlotNumber = 1,
                    WowClassId = 1,
                    WowClass = new WowClass { Id = 1, Name = "Warrior", Color = "C79C6E" },
                    SpecId = 71,
                    Spec = new Spec { Id = 71, Name = "Arms", ClassId = 1, IconUrl = "https://cdn/arms.jpg" },
                    Note = "Bob",
                },
            ],
        };
        _previews.Setup(r => r.GetByIdAsync(PreviewId, GuildBranchId, default)).ReturnsAsync(preview);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsSuccess.Should().BeTrue();
        var response = result.Value!;
        response.Id.Should().Be(PreviewId);
        response.Name.Should().Be("40-man");
        response.GroupCount.Should().Be(8);
        response.SlotsPerGroup.Should().Be(5);
        var slot = response.Slots.Single();
        slot.GroupNumber.Should().Be(1);
        slot.SlotNumber.Should().Be(1);
        slot.WowClassId.Should().Be(1);
        slot.WowClassName.Should().Be("Warrior");
        slot.WowClassColor.Should().Be("#C79C6E");
        slot.SpecId.Should().Be(71);
        slot.SpecName.Should().Be("Arms");
        slot.SpecIconUrl.Should().Be("https://cdn/arms.jpg");
        slot.Note.Should().Be("Bob");
    }

    [Fact]
    public async Task HandleAsync_Success_SlotWithoutClassOrSpec_MapsNullRefFields()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        var preview = new RaidCompositionPreview
        {
            Id = PreviewId,
            Name = "40-man",
            GroupCount = 8,
            SlotsPerGroup = 5,
            Slots = [new RaidCompositionPreviewSlot { GroupNumber = 2, SlotNumber = 3, Note = "Just a note" }],
        };
        _previews.Setup(r => r.GetByIdAsync(PreviewId, GuildBranchId, default)).ReturnsAsync(preview);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        var slot = result.Value!.Slots.Single();
        slot.WowClassId.Should().BeNull();
        slot.WowClassName.Should().BeNull();
        slot.WowClassColor.Should().BeNull();
        slot.SpecId.Should().BeNull();
        slot.SpecName.Should().BeNull();
        slot.SpecIconUrl.Should().BeNull();
        slot.Note.Should().Be("Just a note");
    }
}
