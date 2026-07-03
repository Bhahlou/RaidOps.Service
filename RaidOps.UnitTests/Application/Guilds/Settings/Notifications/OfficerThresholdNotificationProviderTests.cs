using FluentAssertions;
using RaidOps.Application.Implementations.Guilds.Settings.Notifications;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;

namespace RaidOps.UnitTests.Application.Guilds.Settings.Notifications;

public class OfficerThresholdNotificationProviderTests
{
    private readonly OfficerThresholdNotificationProvider _sut = new();

    private const string DiscordId = "user-1";

    private static UserGuild MakeUserGuild(
        string      guildId,
        bool        isAdmin,
        bool        isRegistered,
        string      name       = "Guild Name",
        string?     timezone   = "Europe/Paris",
        RosterMode? rosterMode = RosterMode.Open,
        string?     minOfficerRoleId = null) => new()
    {
        UserDiscordId = DiscordId,
        GuildId       = guildId,
        IsAdmin       = isAdmin,
        Guild = new Guild
        {
            Id               = guildId,
            Name             = name,
            IsRegistered     = isRegistered,
            Timezone         = timezone,
            RosterMode       = rosterMode,
            MinOfficerRoleId = minOfficerRoleId,
        },
    };

    [Fact]
    public async Task GetActiveAsync_AdminOfConfiguredGuildWithoutThreshold_ReturnsNotification()
    {
        var guild = MakeUserGuild("g1", isAdmin: true, isRegistered: true, name: "RaidOps");

        var result = await _sut.GetActiveAsync(DiscordId, [guild], default);

        result.Should().ContainSingle(n =>
            n.Type == NotificationType.OfficerThresholdNotConfigured && n.GuildId == "g1" && n.GuildName == "RaidOps");
    }

    [Fact]
    public async Task GetActiveAsync_ThresholdAlreadyConfigured_NoNotification()
    {
        var guild = MakeUserGuild("g1", isAdmin: true, isRegistered: true, minOfficerRoleId: "role-1");

        var result = await _sut.GetActiveAsync(DiscordId, [guild], default);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveAsync_NotAdmin_NoNotification()
    {
        var guild = MakeUserGuild("g1", isAdmin: false, isRegistered: true);

        var result = await _sut.GetActiveAsync(DiscordId, [guild], default);

        result.Should().BeEmpty();
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
    }

    [Fact]
    public async Task GetActiveAsync_GuildStillOnboarding_RosterModeNull_NoNotification()
    {
        var guild = MakeUserGuild("g1", isAdmin: true, isRegistered: true, rosterMode: null);

        var result = await _sut.GetActiveAsync(DiscordId, [guild], default);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveAsync_MultipleGuilds_ReturnsOnlyMatchingOnes()
    {
        var guilds = new[]
        {
            MakeUserGuild("g-needs-config", isAdmin: true, isRegistered: true),
            MakeUserGuild("g-configured", isAdmin: true, isRegistered: true, minOfficerRoleId: "role-1"),
            MakeUserGuild("g-not-admin", isAdmin: false, isRegistered: true),
        };

        var result = await _sut.GetActiveAsync(DiscordId, guilds, default);

        result.Should().ContainSingle(n => n.GuildId == "g-needs-config");
    }
}
