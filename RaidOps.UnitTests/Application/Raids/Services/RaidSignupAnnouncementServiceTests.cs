using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RaidOps.Application.Contracts.Raids.Signups.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Raids;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Services;

public class RaidSignupAnnouncementServiceTests
{
    private readonly Mock<IGuildNotificationSettingsRepository> _notificationSettingsRepository = new();
    private readonly Mock<IRaidEventRepository> _raidEventRepository = new();
    private readonly Mock<IRaidNotificationContentBuilder> _contentBuilder = new();
    private readonly Mock<IDiscordBotService> _discordBotService = new();
    private readonly Mock<IMessageService> _messages = new();
    private readonly Mock<IRaidSignupResponseBuilder> _raidSignupResponseBuilder = new();
    private readonly Mock<ILogger<RaidSignupAnnouncementService>> _logger = new();
    private readonly RaidSignupAnnouncementService _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const int EventId = 5;
    private static readonly DiscordEmbedContent Embed = new("Raid signup");

    public RaidSignupAnnouncementServiceTests()
    {
        _discordBotService.Setup(d => d.Messages).Returns(_messages.Object);
        _sut = new RaidSignupAnnouncementService(
            _notificationSettingsRepository.Object, _raidEventRepository.Object, _contentBuilder.Object, _discordBotService.Object,
            _raidSignupResponseBuilder.Object, _logger.Object);

        _raidSignupResponseBuilder.Setup(b => b.BuildAsync(It.IsAny<RaidEvent>(), default)).ReturnsAsync([]);
        _contentBuilder.Setup(b => b.BuildSignupCallAsync(GuildId, GuildBranchId, It.IsAny<RaidEvent>(), It.IsAny<IReadOnlyList<RaidSignupResponse>>(), default))
            .ReturnsAsync(Embed);
    }

    private static RaidEvent MakeEvent(string? dedicatedChannelId = null, string? channelId = null, string? messageId = null) => new()
    {
        Id = EventId,
        GuildId = GuildId,
        GuildBranchId = GuildBranchId,
        DedicatedAnnouncementChannelId = dedicatedChannelId,
        SignupCallAnnouncementChannelId = channelId,
        SignupCallAnnouncementMessageId = messageId,
    };

    // ── channel resolution ───────────────────────────────────────────────────

    [Fact]
    public async Task PublishOrUpdateSignupCallAsync_NoDedicatedChannelAndNoGuildSetting_SkipsSilently()
    {
        _notificationSettingsRepository.Setup(r => r.GetAsync(GuildId, GuildNotificationEventType.RaidSignupCallPosted, GuildBranchId, default)).ReturnsAsync((GuildNotificationSetting?)null);

        await _sut.PublishOrUpdateSignupCallAsync(MakeEvent());

        _messages.Verify(m => m.PostEmbedAsync(It.IsAny<ulong>(), It.IsAny<DiscordEmbedContent>(), default), Times.Never);
    }

    [Fact]
    public async Task PublishOrUpdateSignupCallAsync_NoDedicatedChannelAndNoGuildSetting_LoggerEnabled_StillSkipsSilently()
    {
        _logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        _notificationSettingsRepository.Setup(r => r.GetAsync(GuildId, GuildNotificationEventType.RaidSignupCallPosted, GuildBranchId, default)).ReturnsAsync((GuildNotificationSetting?)null);

        await _sut.PublishOrUpdateSignupCallAsync(MakeEvent());

        _messages.Verify(m => m.PostEmbedAsync(It.IsAny<ulong>(), It.IsAny<DiscordEmbedContent>(), default), Times.Never);
    }

    [Fact]
    public async Task PublishOrUpdateSignupCallAsync_GuildSettingDisabled_SkipsSilently()
    {
        _notificationSettingsRepository.Setup(r => r.GetAsync(GuildId, GuildNotificationEventType.RaidSignupCallPosted, GuildBranchId, default))
            .ReturnsAsync(new GuildNotificationSetting { Enabled = false, ChannelId = "999" });

        await _sut.PublishOrUpdateSignupCallAsync(MakeEvent());

        _messages.Verify(m => m.PostEmbedAsync(It.IsAny<ulong>(), It.IsAny<DiscordEmbedContent>(), default), Times.Never);
    }

    [Fact]
    public async Task PublishOrUpdateSignupCallAsync_DedicatedChannelSet_UsesItWithoutCheckingGuildSetting()
    {
        await _sut.PublishOrUpdateSignupCallAsync(MakeEvent(dedicatedChannelId: "111"));

        _messages.Verify(m => m.PostEmbedAsync(111, Embed, default), Times.Once);
        _notificationSettingsRepository.Verify(r => r.GetAsync(It.IsAny<string>(), It.IsAny<GuildNotificationEventType>(), It.IsAny<int?>(), default), Times.Never);
    }

    [Fact]
    public async Task PublishOrUpdateSignupCallAsync_NoDedicatedChannelButGuildSettingEnabled_UsesTheGuildChannel()
    {
        _notificationSettingsRepository.Setup(r => r.GetAsync(GuildId, GuildNotificationEventType.RaidSignupCallPosted, GuildBranchId, default))
            .ReturnsAsync(new GuildNotificationSetting { Enabled = true, ChannelId = "222" });

        await _sut.PublishOrUpdateSignupCallAsync(MakeEvent());

        _messages.Verify(m => m.PostEmbedAsync(222, Embed, default), Times.Once);
    }

    // ── post vs edit ─────────────────────────────────────────────────────────

    [Fact]
    public async Task PublishOrUpdateSignupCallAsync_NoExistingMessage_PostsAndPersistsTheReference()
    {
        _messages.Setup(m => m.PostEmbedAsync(111, Embed, default)).ReturnsAsync(999UL);

        await _sut.PublishOrUpdateSignupCallAsync(MakeEvent(dedicatedChannelId: "111"));

        _messages.Verify(m => m.PostEmbedAsync(111, Embed, default), Times.Once);
        _messages.Verify(m => m.EditEmbedAsync(It.IsAny<ulong>(), It.IsAny<ulong>(), It.IsAny<DiscordEmbedContent>(), default), Times.Never);
        _raidEventRepository.Verify(r => r.UpdateSignupCallAnnouncementReferenceAsync(EventId, GuildBranchId, "111", "999", default), Times.Once);
    }

    [Fact]
    public async Task PublishOrUpdateSignupCallAsync_ExistingMessage_EditsInPlaceWithoutPosting()
    {
        await _sut.PublishOrUpdateSignupCallAsync(MakeEvent(dedicatedChannelId: "111", channelId: "111", messageId: "777"));

        _messages.Verify(m => m.EditEmbedAsync(111, 777, Embed, default), Times.Once);
        _messages.Verify(m => m.PostEmbedAsync(It.IsAny<ulong>(), It.IsAny<DiscordEmbedContent>(), default), Times.Never);
    }

    [Fact]
    public async Task PublishOrUpdateSignupCallAsync_LoggerEnabled_StillPostsNormally()
    {
        _logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        await _sut.PublishOrUpdateSignupCallAsync(MakeEvent(dedicatedChannelId: "111"));

        _messages.Verify(m => m.PostEmbedAsync(111, Embed, default), Times.Once);
    }

    // ── best-effort failure handling ─────────────────────────────────────────

    [Fact]
    public async Task PublishOrUpdateSignupCallAsync_PostFails_SwallowsTheException()
    {
        _messages.Setup(m => m.PostEmbedAsync(It.IsAny<ulong>(), It.IsAny<DiscordEmbedContent>(), default)).ThrowsAsync(new InvalidOperationException("403"));

        var act = () => _sut.PublishOrUpdateSignupCallAsync(MakeEvent(dedicatedChannelId: "111"));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteSignupCallAsync_NoStandingMessage_DoesNothing()
    {
        await _sut.DeleteSignupCallAsync(MakeEvent());

        _messages.Verify(m => m.DeleteMessageAsync(It.IsAny<ulong>(), It.IsAny<ulong>(), default), Times.Never);
    }

    [Fact]
    public async Task DeleteSignupCallAsync_StandingMessage_DeletesIt()
    {
        await _sut.DeleteSignupCallAsync(MakeEvent(channelId: "111", messageId: "777"));

        _messages.Verify(m => m.DeleteMessageAsync(111, 777, default), Times.Once);
    }

    [Fact]
    public async Task DeleteSignupCallAsync_DeleteFails_SwallowsTheException()
    {
        _messages.Setup(m => m.DeleteMessageAsync(It.IsAny<ulong>(), It.IsAny<ulong>(), default)).ThrowsAsync(new InvalidOperationException("already gone"));

        var act = () => _sut.DeleteSignupCallAsync(MakeEvent(channelId: "111", messageId: "777"));

        await act.Should().NotThrowAsync();
    }
}
