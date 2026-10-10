using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Notifications.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Notifications.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Notifications.Services;

public class UserNotificationServiceTests
{
    private readonly Mock<INotificationDismissalRepository> _dismissals = new();

    private const string DiscordId = "user-1";

    private static readonly IReadOnlyList<UserGuild> EligibleGuilds = [];

    private UserNotificationService MakeSut(params INotificationSignalProvider[] providers)
        => new(providers, _dismissals.Object);

    private static Mock<INotificationSignalProvider> MakeProvider(params NotificationResponse[] notifications)
    {
        var provider = new Mock<INotificationSignalProvider>();
        provider.Setup(p => p.GetActiveAsync(DiscordId, EligibleGuilds, default)).ReturnsAsync(notifications.ToList());
        return provider;
    }

    private static NotificationResponse MakeNotification(string guildId, NotificationType type = NotificationType.BranchOfficerRolesNotConfigured)
        => new() { Type = type, GuildId = guildId, GuildName = $"Guild {guildId}" };

    public UserNotificationServiceTests()
    {
        _dismissals.Setup(d => d.GetDismissedKeysAsync(DiscordId, default)).ReturnsAsync([]);
    }

    [Fact]
    public async Task GetActiveNotificationsAsync_NoProviders_ReturnsEmpty()
    {
        var sut = MakeSut();

        var result = await sut.GetActiveNotificationsAsync(DiscordId, EligibleGuilds, default);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveNotificationsAsync_SingleProvider_ReturnsItsNotifications()
    {
        var notification = MakeNotification("g1");
        var sut = MakeSut(MakeProvider(notification).Object);

        var result = await sut.GetActiveNotificationsAsync(DiscordId, EligibleGuilds, default);

        result.Should().ContainSingle().Which.Should().Be(notification);
    }

    [Fact]
    public async Task GetActiveNotificationsAsync_MultipleProviders_AggregatesAll()
    {
        var providerA = MakeProvider(MakeNotification("g1"));
        var providerB = MakeProvider(MakeNotification("g2"));
        var sut = MakeSut(providerA.Object, providerB.Object);

        var result = await sut.GetActiveNotificationsAsync(DiscordId, EligibleGuilds, default);

        result.Should().HaveCount(2);
        result.Select(n => n.GuildId).Should().BeEquivalentTo(["g1", "g2"]);
    }

    [Fact]
    public async Task GetActiveNotificationsAsync_DismissedNotification_IsFilteredOut()
    {
        var dismissed = MakeNotification("g1");
        var kept = MakeNotification("g2");
        _dismissals.Setup(d => d.GetDismissedKeysAsync(DiscordId, default))
            .ReturnsAsync([(NotificationType.BranchOfficerRolesNotConfigured, "g1")]);
        var sut = MakeSut(MakeProvider(dismissed, kept).Object);

        var result = await sut.GetActiveNotificationsAsync(DiscordId, EligibleGuilds, default);

        result.Should().ContainSingle().Which.GuildId.Should().Be("g2");
    }

    [Fact]
    public async Task GetActiveNotificationsAsync_DismissalMatchesOnTypeAndGuildTogether()
    {
        // Same GuildId but a different Type must not be filtered out by a dismissal for another type.
        var notification = MakeNotification("g1", NotificationType.BranchOfficerRolesNotConfigured);
        _dismissals.Setup(d => d.GetDismissedKeysAsync(DiscordId, default))
            .ReturnsAsync([(NotificationType.BranchOfficerRolesNotConfigured, "g2")]);
        var sut = MakeSut(MakeProvider(notification).Object);

        var result = await sut.GetActiveNotificationsAsync(DiscordId, EligibleGuilds, default);

        result.Should().ContainSingle().Which.GuildId.Should().Be("g1");
    }
}
