using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Raids;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Services;

public class RaidCompositionAnnouncementServiceTests
{
    private readonly Mock<IGuildNotificationSettingsRepository> _notificationSettingsRepository = new();
    private readonly Mock<IRaidCompositionRepository> _raidCompositionRepository = new();
    private readonly Mock<IRaidEventRepository> _raidEventRepository = new();
    private readonly Mock<IRaidNotificationContentBuilder> _contentBuilder = new();
    private readonly Mock<IDiscordBotService> _discordBotService = new();
    private readonly Mock<IMessageService> _messages = new();
    private readonly Mock<ILogger<RaidCompositionAnnouncementService>> _logger = new();
    private readonly RaidCompositionAnnouncementService _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const int EventId = 5;
    private const string PlayerDiscordId = "42";

    public RaidCompositionAnnouncementServiceTests()
    {
        _discordBotService.Setup(d => d.Messages).Returns(_messages.Object);
        _sut = new RaidCompositionAnnouncementService(
            _notificationSettingsRepository.Object, _raidCompositionRepository.Object, _raidEventRepository.Object,
            _contentBuilder.Object, _discordBotService.Object, _logger.Object);
    }

    private static RaidEvent MakeEvent(string? channelId = null, string? messageId = null) => new()
    {
        Id = EventId,
        GuildId = GuildId,
        GuildBranchId = GuildBranchId,
        Name = "Split 1",
        CompositionAnnouncementChannelId = channelId,
        CompositionAnnouncementMessageId = messageId,
    };

    private static RaidCharacterRef MakeCharacter() => new("Arthas", 6, "Blood");

    private void SetupPostedSetting(bool enabled = true, string? channelId = "999") =>
        _notificationSettingsRepository.Setup(r => r.GetAsync(GuildId, GuildNotificationEventType.RaidCompositionAnnouncementPosted, GuildBranchId, default))
            .ReturnsAsync(new GuildNotificationSetting { GuildId = GuildId, EventType = GuildNotificationEventType.RaidCompositionAnnouncementPosted, Enabled = enabled, ChannelId = channelId });

    private void SetupDmSetting(bool enabled = true) =>
        _notificationSettingsRepository.Setup(r => r.GetAsync(GuildId, GuildNotificationEventType.RaidCompositionAnnouncementDm, GuildBranchId, default))
            .ReturnsAsync(new GuildNotificationSetting { GuildId = GuildId, EventType = GuildNotificationEventType.RaidCompositionAnnouncementDm, Enabled = enabled });

    // ── PublishOrUpdateAnnouncementAsync ──────────────────────────────────────

    [Fact]
    public async Task PublishOrUpdateAnnouncementAsync_NoSettingRow_DoesNothing()
    {
        _notificationSettingsRepository.Setup(r => r.GetAsync(GuildId, GuildNotificationEventType.RaidCompositionAnnouncementPosted, GuildBranchId, default))
            .ReturnsAsync((GuildNotificationSetting?)null);

        await _sut.PublishOrUpdateAnnouncementAsync(MakeEvent());

        _messages.Verify(m => m.PostEmbedAsync(It.IsAny<ulong>(), It.IsAny<DiscordEmbedContent>(), default), Times.Never);
        _messages.Verify(m => m.EditEmbedAsync(It.IsAny<ulong>(), It.IsAny<ulong>(), It.IsAny<DiscordEmbedContent>(), default), Times.Never);
    }

    [Fact]
    public async Task PublishOrUpdateAnnouncementAsync_SettingDisabled_DoesNothing()
    {
        SetupPostedSetting(enabled: false);

        await _sut.PublishOrUpdateAnnouncementAsync(MakeEvent());

        _messages.Verify(m => m.PostEmbedAsync(It.IsAny<ulong>(), It.IsAny<DiscordEmbedContent>(), default), Times.Never);
    }

    [Fact]
    public async Task PublishOrUpdateAnnouncementAsync_SettingEnabledButNoChannel_DoesNothing()
    {
        SetupPostedSetting(channelId: null);

        await _sut.PublishOrUpdateAnnouncementAsync(MakeEvent());

        _messages.Verify(m => m.PostEmbedAsync(It.IsAny<ulong>(), It.IsAny<DiscordEmbedContent>(), default), Times.Never);
    }

    [Fact]
    public async Task PublishOrUpdateAnnouncementAsync_NoStoredMessageReference_PostsNewEmbedAndStoresReference()
    {
        SetupPostedSetting(channelId: "999");
        var raidEvent = MakeEvent();
        _raidCompositionRepository.Setup(r => r.GetAssignmentsForEventAsync(EventId, default)).ReturnsAsync([]);
        var embed = new DiscordEmbedContent("Split 1");
        _contentBuilder.Setup(b => b.BuildCompositionAnnouncementAsync(GuildId, raidEvent, It.IsAny<IReadOnlyList<RaidSlotAssignment>>(), default)).ReturnsAsync(embed);
        _messages.Setup(m => m.PostEmbedAsync(999, embed, default)).ReturnsAsync(12345UL);

        await _sut.PublishOrUpdateAnnouncementAsync(raidEvent);

        _messages.Verify(m => m.PostEmbedAsync(999, embed, default), Times.Once);
        _messages.Verify(m => m.EditEmbedAsync(It.IsAny<ulong>(), It.IsAny<ulong>(), It.IsAny<DiscordEmbedContent>(), default), Times.Never);
        _raidEventRepository.Verify(r => r.UpdateCompositionAnnouncementReferenceAsync(EventId, GuildBranchId, "999", "12345", default), Times.Once);
    }

    [Fact]
    public async Task PublishOrUpdateAnnouncementAsync_ExistingMessageReference_EditsInPlaceInsteadOfPosting()
    {
        SetupPostedSetting(channelId: "999");
        var raidEvent = MakeEvent(channelId: "111", messageId: "222");
        _raidCompositionRepository.Setup(r => r.GetAssignmentsForEventAsync(EventId, default)).ReturnsAsync([]);
        var embed = new DiscordEmbedContent("Split 1");
        _contentBuilder.Setup(b => b.BuildCompositionAnnouncementAsync(GuildId, raidEvent, It.IsAny<IReadOnlyList<RaidSlotAssignment>>(), default)).ReturnsAsync(embed);

        await _sut.PublishOrUpdateAnnouncementAsync(raidEvent);

        _messages.Verify(m => m.EditEmbedAsync(111, 222, embed, default), Times.Once);
        _messages.Verify(m => m.PostEmbedAsync(It.IsAny<ulong>(), It.IsAny<DiscordEmbedContent>(), default), Times.Never);
        _raidEventRepository.Verify(r => r.UpdateCompositionAnnouncementReferenceAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task PublishOrUpdateAnnouncementAsync_PostThrows_SwallowsException()
    {
        SetupPostedSetting(channelId: "999");
        _raidCompositionRepository.Setup(r => r.GetAssignmentsForEventAsync(EventId, default)).ReturnsAsync([]);
        _contentBuilder.Setup(b => b.BuildCompositionAnnouncementAsync(GuildId, It.IsAny<RaidEvent>(), It.IsAny<IReadOnlyList<RaidSlotAssignment>>(), default))
            .ReturnsAsync(new DiscordEmbedContent("Split 1"));
        _messages.Setup(m => m.PostEmbedAsync(It.IsAny<ulong>(), It.IsAny<DiscordEmbedContent>(), default)).ThrowsAsync(new InvalidOperationException("boom"));

        var act = () => _sut.PublishOrUpdateAnnouncementAsync(MakeEvent());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishOrUpdateAnnouncementAsync_EditThrows_SwallowsException()
    {
        SetupPostedSetting(channelId: "999");
        _raidCompositionRepository.Setup(r => r.GetAssignmentsForEventAsync(EventId, default)).ReturnsAsync([]);
        _contentBuilder.Setup(b => b.BuildCompositionAnnouncementAsync(GuildId, It.IsAny<RaidEvent>(), It.IsAny<IReadOnlyList<RaidSlotAssignment>>(), default))
            .ReturnsAsync(new DiscordEmbedContent("Split 1"));
        _messages.Setup(m => m.EditEmbedAsync(It.IsAny<ulong>(), It.IsAny<ulong>(), It.IsAny<DiscordEmbedContent>(), default)).ThrowsAsync(new InvalidOperationException("boom"));

        var act = () => _sut.PublishOrUpdateAnnouncementAsync(MakeEvent(channelId: "111", messageId: "222"));

        await act.Should().NotThrowAsync();
    }

    // ── NotifyPlayerAddedAsync / NotifyPlayerRemovedAsync / NotifyPlayerSpecChangedAsync ──

    [Fact]
    public async Task NotifyPlayerAddedAsync_DmSettingDisabled_DoesNotSendDm()
    {
        SetupDmSetting(enabled: false);

        await _sut.NotifyPlayerAddedAsync(MakeEvent(), PlayerDiscordId, MakeCharacter(), isInitialPublish: false);

        _messages.Verify(m => m.SendDirectMessageEmbedAsync(It.IsAny<ulong>(), It.IsAny<DiscordEmbedContent>(), default), Times.Never);
    }

    [Fact]
    public async Task NotifyPlayerAddedAsync_NoDmSettingRow_DoesNotSendDm()
    {
        _notificationSettingsRepository.Setup(r => r.GetAsync(GuildId, GuildNotificationEventType.RaidCompositionAnnouncementDm, GuildBranchId, default))
            .ReturnsAsync((GuildNotificationSetting?)null);

        await _sut.NotifyPlayerAddedAsync(MakeEvent(), PlayerDiscordId, MakeCharacter(), isInitialPublish: false);

        _messages.Verify(m => m.SendDirectMessageEmbedAsync(It.IsAny<ulong>(), It.IsAny<DiscordEmbedContent>(), default), Times.Never);
    }

    [Fact]
    public async Task NotifyPlayerAddedAsync_DmSettingEnabled_SendsDmToPlayer()
    {
        SetupDmSetting();
        var raidEvent = MakeEvent();
        var character = MakeCharacter();
        var embed = new DiscordEmbedContent("Added");
        _contentBuilder.Setup(b => b.BuildPlayerAddedDmAsync(GuildId, raidEvent, character, false, default)).ReturnsAsync(embed);

        await _sut.NotifyPlayerAddedAsync(raidEvent, PlayerDiscordId, character, isInitialPublish: false);

        _messages.Verify(m => m.SendDirectMessageEmbedAsync(ulong.Parse(PlayerDiscordId), embed, default), Times.Once);
    }

    [Fact]
    public async Task NotifyPlayerAddedAsync_InitialPublish_PassesFlagThroughToContentBuilder()
    {
        SetupDmSetting();
        var raidEvent = MakeEvent();
        var character = MakeCharacter();
        _contentBuilder.Setup(b => b.BuildPlayerAddedDmAsync(GuildId, raidEvent, character, true, default)).ReturnsAsync(new DiscordEmbedContent("Published"));

        await _sut.NotifyPlayerAddedAsync(raidEvent, PlayerDiscordId, character, isInitialPublish: true);

        _contentBuilder.Verify(b => b.BuildPlayerAddedDmAsync(GuildId, raidEvent, character, true, default), Times.Once);
    }

    [Fact]
    public async Task NotifyPlayerAddedAsync_DmThrows_SwallowsException()
    {
        SetupDmSetting();
        _contentBuilder.Setup(b => b.BuildPlayerAddedDmAsync(GuildId, It.IsAny<RaidEvent>(), It.IsAny<RaidCharacterRef>(), It.IsAny<bool>(), default))
            .ReturnsAsync(new DiscordEmbedContent("Added"));
        _messages.Setup(m => m.SendDirectMessageEmbedAsync(It.IsAny<ulong>(), It.IsAny<DiscordEmbedContent>(), default)).ThrowsAsync(new InvalidOperationException("boom"));

        var act = () => _sut.NotifyPlayerAddedAsync(MakeEvent(), PlayerDiscordId, MakeCharacter(), isInitialPublish: false);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task NotifyPlayerRemovedAsync_DmSettingEnabled_SendsDmToPlayer()
    {
        SetupDmSetting();
        var raidEvent = MakeEvent();
        var character = MakeCharacter();
        var embed = new DiscordEmbedContent("Removed");
        _contentBuilder.Setup(b => b.BuildPlayerRemovedDmAsync(GuildId, raidEvent, character, default)).ReturnsAsync(embed);

        await _sut.NotifyPlayerRemovedAsync(raidEvent, PlayerDiscordId, character);

        _messages.Verify(m => m.SendDirectMessageEmbedAsync(ulong.Parse(PlayerDiscordId), embed, default), Times.Once);
    }

    [Fact]
    public async Task NotifyPlayerRemovedAsync_DmSettingDisabled_DoesNotSendDm()
    {
        SetupDmSetting(enabled: false);

        await _sut.NotifyPlayerRemovedAsync(MakeEvent(), PlayerDiscordId, MakeCharacter());

        _messages.Verify(m => m.SendDirectMessageEmbedAsync(It.IsAny<ulong>(), It.IsAny<DiscordEmbedContent>(), default), Times.Never);
    }

    [Fact]
    public async Task NotifyPlayerSpecChangedAsync_DmSettingEnabled_SendsDmToPlayer()
    {
        SetupDmSetting();
        var raidEvent = MakeEvent();
        var character = MakeCharacter();
        var embed = new DiscordEmbedContent("Spec changed");
        _contentBuilder.Setup(b => b.BuildPlayerSpecChangedDmAsync(GuildId, raidEvent, character, "Blood", "Frost", default)).ReturnsAsync(embed);

        await _sut.NotifyPlayerSpecChangedAsync(raidEvent, PlayerDiscordId, character, "Blood", "Frost");

        _messages.Verify(m => m.SendDirectMessageEmbedAsync(ulong.Parse(PlayerDiscordId), embed, default), Times.Once);
    }

    [Fact]
    public async Task NotifyPlayerSpecChangedAsync_DmSettingDisabled_DoesNotSendDm()
    {
        SetupDmSetting(enabled: false);

        await _sut.NotifyPlayerSpecChangedAsync(MakeEvent(), PlayerDiscordId, MakeCharacter(), "Blood", "Frost");

        _messages.Verify(m => m.SendDirectMessageEmbedAsync(It.IsAny<ulong>(), It.IsAny<DiscordEmbedContent>(), default), Times.Never);
    }

    // ── DeleteAnnouncementAsync ────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAnnouncementAsync_NoStoredReference_DoesNothing()
    {
        await _sut.DeleteAnnouncementAsync(MakeEvent());

        _messages.Verify(m => m.DeleteMessageAsync(It.IsAny<ulong>(), It.IsAny<ulong>(), default), Times.Never);
    }

    [Fact]
    public async Task DeleteAnnouncementAsync_WithStoredReference_DeletesMessage()
    {
        await _sut.DeleteAnnouncementAsync(MakeEvent(channelId: "111", messageId: "222"));

        _messages.Verify(m => m.DeleteMessageAsync(111, 222, default), Times.Once);
    }

    [Fact]
    public async Task DeleteAnnouncementAsync_DeleteThrows_SwallowsException()
    {
        _messages.Setup(m => m.DeleteMessageAsync(It.IsAny<ulong>(), It.IsAny<ulong>(), default)).ThrowsAsync(new InvalidOperationException("boom"));

        var act = () => _sut.DeleteAnnouncementAsync(MakeEvent(channelId: "111", messageId: "222"));

        await act.Should().NotThrowAsync();
    }

    // ── NotifyPlayerRaidCancelledAsync ────────────────────────────────────────

    [Fact]
    public async Task NotifyPlayerRaidCancelledAsync_IgnoresNotificationSetting_AlwaysSendsDm()
    {
        var raidEvent = MakeEvent();
        var character = MakeCharacter();
        var embed = new DiscordEmbedContent("Cancelled");
        _contentBuilder.Setup(b => b.BuildRaidCancelledDmAsync(GuildId, raidEvent, character, default)).ReturnsAsync(embed);

        await _sut.NotifyPlayerRaidCancelledAsync(raidEvent, PlayerDiscordId, character);

        _messages.Verify(m => m.SendDirectMessageEmbedAsync(ulong.Parse(PlayerDiscordId), embed, default), Times.Once);
        _notificationSettingsRepository.Verify(r => r.GetAsync(It.IsAny<string>(), It.IsAny<GuildNotificationEventType>(), It.IsAny<int?>(), default), Times.Never);
    }

    [Fact]
    public async Task NotifyPlayerRaidCancelledAsync_DmThrows_SwallowsException()
    {
        _contentBuilder.Setup(b => b.BuildRaidCancelledDmAsync(GuildId, It.IsAny<RaidEvent>(), It.IsAny<RaidCharacterRef>(), default))
            .ReturnsAsync(new DiscordEmbedContent("Cancelled"));
        _messages.Setup(m => m.SendDirectMessageEmbedAsync(It.IsAny<ulong>(), It.IsAny<DiscordEmbedContent>(), default)).ThrowsAsync(new InvalidOperationException("boom"));

        var act = () => _sut.NotifyPlayerRaidCancelledAsync(MakeEvent(), PlayerDiscordId, MakeCharacter());

        await act.Should().NotThrowAsync();
    }
}
