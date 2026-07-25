using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RaidOps.Application.Implementations.Guilds.Notifications;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Guilds.Notifications;

public class GuildNotificationDispatcherTests
{
    private readonly Mock<IGuildNotificationSettingsRepository> _settingsRepository = new();
    private readonly Mock<IDiscordBotService> _discordBotService = new();
    private readonly Mock<IMessageService> _messages = new();
    private readonly Mock<ILogger<GuildNotificationDispatcher>> _logger = new();
    private readonly GuildNotificationDispatcher _sut;

    private const string GuildId = "guild-1";
    private const string ChannelId = "123456789";

    private static readonly DiscordEmbedContent Embed = new("Title");

    public GuildNotificationDispatcherTests()
    {
        _discordBotService.Setup(d => d.Messages).Returns(_messages.Object);
        _sut = new GuildNotificationDispatcher(_settingsRepository.Object, _discordBotService.Object, _logger.Object);
    }

    [Fact]
    public async Task NotifyAsync_NoSettingRow_DoesNotSend()
    {
        _settingsRepository.Setup(r => r.GetAsync(GuildId, GuildNotificationEventType.AbsenceAdded, default))
            .ReturnsAsync((GuildNotificationSetting?)null);

        await _sut.NotifyAsync(GuildId, GuildNotificationEventType.AbsenceAdded, Embed);

        _messages.Verify(m => m.SendEmbedAsync(It.IsAny<ulong>(), It.IsAny<DiscordEmbedContent>(), default), Times.Never);
    }

    [Fact]
    public async Task NotifyAsync_SettingDisabled_DoesNotSend()
    {
        _settingsRepository.Setup(r => r.GetAsync(GuildId, GuildNotificationEventType.AbsenceAdded, default))
            .ReturnsAsync(new GuildNotificationSetting { GuildId = GuildId, EventType = GuildNotificationEventType.AbsenceAdded, Enabled = false, ChannelId = ChannelId });

        await _sut.NotifyAsync(GuildId, GuildNotificationEventType.AbsenceAdded, Embed);

        _messages.Verify(m => m.SendEmbedAsync(It.IsAny<ulong>(), It.IsAny<DiscordEmbedContent>(), default), Times.Never);
    }

    [Fact]
    public async Task NotifyAsync_EnabledWithoutChannel_DoesNotSend()
    {
        _settingsRepository.Setup(r => r.GetAsync(GuildId, GuildNotificationEventType.AbsenceAdded, default))
            .ReturnsAsync(new GuildNotificationSetting { GuildId = GuildId, EventType = GuildNotificationEventType.AbsenceAdded, Enabled = true, ChannelId = null });

        await _sut.NotifyAsync(GuildId, GuildNotificationEventType.AbsenceAdded, Embed);

        _messages.Verify(m => m.SendEmbedAsync(It.IsAny<ulong>(), It.IsAny<DiscordEmbedContent>(), default), Times.Never);
    }

    [Fact]
    public async Task NotifyAsync_EnabledWithChannel_SendsToParsedChannelId()
    {
        _settingsRepository.Setup(r => r.GetAsync(GuildId, GuildNotificationEventType.AbsenceAdded, default))
            .ReturnsAsync(new GuildNotificationSetting { GuildId = GuildId, EventType = GuildNotificationEventType.AbsenceAdded, Enabled = true, ChannelId = ChannelId });

        await _sut.NotifyAsync(GuildId, GuildNotificationEventType.AbsenceAdded, Embed);

        _messages.Verify(m => m.SendEmbedAsync(ulong.Parse(ChannelId), Embed, default), Times.Once);
    }

    [Fact]
    public async Task NotifyAsync_SendThrows_ExceptionIsSwallowed()
    {
        _settingsRepository.Setup(r => r.GetAsync(GuildId, GuildNotificationEventType.AbsenceAdded, default))
            .ReturnsAsync(new GuildNotificationSetting { GuildId = GuildId, EventType = GuildNotificationEventType.AbsenceAdded, Enabled = true, ChannelId = ChannelId });
        _messages.Setup(m => m.SendEmbedAsync(It.IsAny<ulong>(), It.IsAny<DiscordEmbedContent>(), default))
            .ThrowsAsync(new InvalidOperationException("missing permissions"));

        var act = async () => await _sut.NotifyAsync(GuildId, GuildNotificationEventType.AbsenceAdded, Embed);

        await act.Should().NotThrowAsync();
    }
}
