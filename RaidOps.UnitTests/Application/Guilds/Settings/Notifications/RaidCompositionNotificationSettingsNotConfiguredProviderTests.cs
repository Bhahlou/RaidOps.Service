using FluentAssertions;
using Moq;
using RaidOps.Application.Implementations.Guilds.Settings.Notifications;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Guilds.Settings.Notifications;

public class RaidCompositionNotificationSettingsNotConfiguredProviderTests
{
    private readonly Mock<IGuildNotificationSettingsRepository> _notificationSettings = new();
    private readonly RaidCompositionNotificationSettingsNotConfiguredProvider _sut;

    private const string DiscordId = "user-1";

    public RaidCompositionNotificationSettingsNotConfiguredProviderTests()
    {
        _sut = new RaidCompositionNotificationSettingsNotConfiguredProvider(_notificationSettings.Object);
    }

    private static UserGuild MakeUserGuild(
        string guildId,
        bool isAdmin,
        bool isRegistered,
        string name = "Guild Name",
        string? timezone = "Europe/Paris") => new()
    {
        UserDiscordId = DiscordId,
        GuildId = guildId,
        IsAdmin = isAdmin,
        Guild = new Guild { Id = guildId, Name = name, IsRegistered = isRegistered, Timezone = timezone },
    };

    [Fact]
    public async Task GetActiveAsync_AdminOfConfiguredGuildWithNoSavedRow_ReturnsNotification()
    {
        var guild = MakeUserGuild("g1", isAdmin: true, isRegistered: true, name: "RaidOps");
        _notificationSettings.Setup(r => r.GetAllForGuildAsync("g1", default)).ReturnsAsync([]);

        var result = await _sut.GetActiveAsync(DiscordId, [guild], default);

        result.Should().ContainSingle(n =>
            n.Type == NotificationType.RaidCompositionNotificationsNotConfigured && n.GuildId == "g1" && n.GuildName == "RaidOps");
    }

    [Theory]
    [InlineData(GuildNotificationEventType.RaidSlotAssigned)]
    [InlineData(GuildNotificationEventType.RaidSlotUnassigned)]
    [InlineData(GuildNotificationEventType.RaidSlotsSwapped)]
    [InlineData(GuildNotificationEventType.RaidSlotSpecChanged)]
    public async Task GetActiveAsync_RowExistsForAnyCompositionFamilyEventEvenIfDisabled_NoNotification(GuildNotificationEventType eventType)
    {
        var guild = MakeUserGuild("g1", isAdmin: true, isRegistered: true);
        _notificationSettings.Setup(r => r.GetAllForGuildAsync("g1", default)).ReturnsAsync(
        [
            new GuildNotificationSetting { GuildId = "g1", EventType = eventType, Enabled = false, ChannelId = null },
        ]);

        var result = await _sut.GetActiveAsync(DiscordId, [guild], default);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveAsync_RowExistsOnlyForUnrelatedFamily_ReturnsNotification()
    {
        var guild = MakeUserGuild("g1", isAdmin: true, isRegistered: true);
        _notificationSettings.Setup(r => r.GetAllForGuildAsync("g1", default)).ReturnsAsync(
        [
            new GuildNotificationSetting { GuildId = "g1", EventType = GuildNotificationEventType.RaidPublished, Enabled = true, ChannelId = "chan-1" },
        ]);

        var result = await _sut.GetActiveAsync(DiscordId, [guild], default);

        result.Should().ContainSingle(n => n.Type == NotificationType.RaidCompositionNotificationsNotConfigured);
    }

    [Fact]
    public async Task GetActiveAsync_NotAdmin_NoNotification()
    {
        var guild = MakeUserGuild("g1", isAdmin: false, isRegistered: true);

        var result = await _sut.GetActiveAsync(DiscordId, [guild], default);

        result.Should().BeEmpty();
        _notificationSettings.Verify(r => r.GetAllForGuildAsync(It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task GetActiveAsync_GuildNotRegistered_NoNotification()
    {
        var guild = MakeUserGuild("g1", isAdmin: true, isRegistered: false);

        var result = await _sut.GetActiveAsync(DiscordId, [guild], default);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveAsync_GuildStillOnboarding_TimezoneNull_NoNotification()
    {
        var guild = MakeUserGuild("g1", isAdmin: true, isRegistered: true, timezone: null);

        var result = await _sut.GetActiveAsync(DiscordId, [guild], default);

        result.Should().BeEmpty();
        _notificationSettings.Verify(r => r.GetAllForGuildAsync(It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task GetActiveAsync_MultipleGuilds_ReturnsOnlyMatchingOnes()
    {
        var guilds = new[]
        {
            MakeUserGuild("g-needs-config", isAdmin: true, isRegistered: true),
            MakeUserGuild("g-configured", isAdmin: true, isRegistered: true),
            MakeUserGuild("g-not-admin", isAdmin: false, isRegistered: true),
        };
        _notificationSettings.Setup(r => r.GetAllForGuildAsync("g-needs-config", default)).ReturnsAsync([]);
        _notificationSettings.Setup(r => r.GetAllForGuildAsync("g-configured", default)).ReturnsAsync(
        [
            new GuildNotificationSetting { GuildId = "g-configured", EventType = GuildNotificationEventType.RaidSlotAssigned, Enabled = true, ChannelId = "chan-1" },
        ]);

        var result = await _sut.GetActiveAsync(DiscordId, guilds, default);

        result.Should().ContainSingle(n => n.GuildId == "g-needs-config");
    }
}
