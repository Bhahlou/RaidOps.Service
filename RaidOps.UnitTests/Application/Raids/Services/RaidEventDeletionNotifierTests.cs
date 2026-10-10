using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Raids;
using RaidOps.Domain.Models.Reference;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

namespace RaidOps.UnitTests.Application.Raids.Services;

public class RaidEventDeletionNotifierTests
{
    private readonly Mock<IGuildNotificationDispatcher> _guildNotificationDispatcher = new();
    private readonly Mock<IRaidNotificationContentBuilder> _raidNotificationContentBuilder = new();
    private readonly Mock<IRaidCompositionAnnouncementService> _raidCompositionAnnouncementService = new();
    private readonly Mock<IRaidSignupAnnouncementService> _raidSignupAnnouncementService = new();
    private readonly RaidEventDeletionNotifier _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "officer-1";

    public RaidEventDeletionNotifierTests()
    {
        _sut = new RaidEventDeletionNotifier(
            _guildNotificationDispatcher.Object, _raidNotificationContentBuilder.Object,
            _raidCompositionAnnouncementService.Object, _raidSignupAnnouncementService.Object);
    }

    [Fact]
    public async Task NotifyAsync_DraftEvent_DoesNotNotify()
    {
        var existing = new RaidEvent { Id = 5, Name = "Split 1", PublicationStatus = RaidPublicationStatus.Draft };

        await _sut.NotifyAsync(GuildId, RequesterId, GuildBranchId, existing);

        _guildNotificationDispatcher.Verify(d => d.NotifyAsync(
            It.IsAny<string>(), It.IsAny<GuildNotificationEventType>(), It.IsAny<int?>(), It.IsAny<DiscordEmbedContent>(), default), Times.Never);
        _raidCompositionAnnouncementService.Verify(s => s.DeleteAnnouncementAsync(It.IsAny<RaidEvent>(), default), Times.Never);
    }

    [Fact]
    public async Task NotifyAsync_PublishedEvent_BuildsAndDispatchesCancelledNotification()
    {
        var existing = new RaidEvent { Id = 5, Name = "Split 1", PublicationStatus = RaidPublicationStatus.Published };
        var embed = new DiscordEmbedContent("Raid cancelled");
        _raidNotificationContentBuilder.Setup(b => b.BuildCancelledAsync(GuildId, RequesterId, existing, default)).ReturnsAsync(embed);

        await _sut.NotifyAsync(GuildId, RequesterId, GuildBranchId, existing);

        _guildNotificationDispatcher.Verify(d => d.NotifyAsync(GuildId, GuildNotificationEventType.RaidCancelled, GuildBranchId, embed, default), Times.Once);
    }

    [Fact]
    public async Task NotifyAsync_PublishedEvent_DeletesAnnouncementAndNotifiesEveryAssignedPlayerOfCancellation()
    {
        var assignments = new List<RaidSlotAssignment>
        {
            new()
            {
                AssignedPlayerDiscordId = "player-1",
                Character = new Character { Id = 1, Name = "Arthas", ClassId = 6 },
                Spec = new Spec { Name = "Blood" },
            },
            new()
            {
                AssignedPlayerDiscordId = "player-2",
                Character = new Character { Id = 2, Name = "Jaina", ClassId = 8 },
                Spec = new Spec { Name = "Frost" },
            },
        };
        var existing = new RaidEvent { Id = 5, Name = "Split 1", PublicationStatus = RaidPublicationStatus.Published, Assignments = assignments };
        _raidNotificationContentBuilder.Setup(b => b.BuildCancelledAsync(GuildId, RequesterId, existing, default)).ReturnsAsync(new DiscordEmbedContent("Raid cancelled"));

        await _sut.NotifyAsync(GuildId, RequesterId, GuildBranchId, existing);

        _raidCompositionAnnouncementService.Verify(s => s.DeleteAnnouncementAsync(existing, default), Times.Once);
        _raidCompositionAnnouncementService.Verify(s => s.NotifyPlayerRaidCancelledAsync(
            existing, "player-1", It.Is<RaidCharacterRef>(c => c.Name == "Arthas" && c.ClassId == 6 && c.SpecName == "Blood"), default), Times.Once);
        _raidCompositionAnnouncementService.Verify(s => s.NotifyPlayerRaidCancelledAsync(
            existing, "player-2", It.Is<RaidCharacterRef>(c => c.Name == "Jaina" && c.ClassId == 8 && c.SpecName == "Frost"), default), Times.Once);
    }

    [Fact]
    public async Task NotifyAsync_SignupModeEvent_DeletesSignupCall()
    {
        var existing = new RaidEvent { Id = 5, Name = "Split 1", PublicationStatus = RaidPublicationStatus.Draft, SignupMode = SignupMode.Signup };

        await _sut.NotifyAsync(GuildId, RequesterId, GuildBranchId, existing);

        _raidSignupAnnouncementService.Verify(s => s.DeleteSignupCallAsync(existing, default), Times.Once);
    }

    [Fact]
    public async Task NotifyAsync_NonSignupModeEvent_DoesNotDeleteSignupCall()
    {
        var existing = new RaidEvent { Id = 5, Name = "Split 1", PublicationStatus = RaidPublicationStatus.Draft, SignupMode = SignupMode.DefaultPresent };

        await _sut.NotifyAsync(GuildId, RequesterId, GuildBranchId, existing);

        _raidSignupAnnouncementService.Verify(s => s.DeleteSignupCallAsync(It.IsAny<RaidEvent>(), default), Times.Never);
    }
}
