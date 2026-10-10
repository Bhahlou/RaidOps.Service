using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Guilds.Settings.Queries;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Guilds.Settings.QueryHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Guilds.Settings.QueryHandlers;

public class GetGuildNotificationSettingsQueryHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IGuildNotificationSettingsRepository> _notificationSettings = new();
    private readonly GetGuildNotificationSettingsQueryHandler _sut;

    private const string GuildId = "guild-1";
    private const string RequesterId = "user-1";

    private static readonly GetGuildNotificationSettingsQuery Query = new() { GuildId = GuildId, RequesterDiscordId = RequesterId };

    public GetGuildNotificationSettingsQueryHandlerTests()
    {
        _sut = new GetGuildNotificationSettingsQueryHandler(_access.Object, _notificationSettings.Object);
    }

    [Fact]
    public async Task HandleAsync_RequesterNotOfficer_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_Success_ReturnsOneRowPerEventTypeDefaultingUnpersistedToDisabled()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _notificationSettings.Setup(r => r.GetEffectiveForGuildAsync(GuildId, null, default)).ReturnsAsync(
        [
            new GuildNotificationSetting { GuildId = GuildId, EventType = GuildNotificationEventType.AbsenceAdded, Enabled = true, ChannelId = "chan-1" },
        ]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(Enum.GetValues<GuildNotificationEventType>().Length);
        result.Value.Should().ContainSingle(r => r.EventType == GuildNotificationEventType.AbsenceAdded && r.Enabled && r.ChannelId == "chan-1");
        result.Value.Should().ContainSingle(r => r.EventType == GuildNotificationEventType.AbsenceRemoved && !r.Enabled && r.ChannelId == null);
    }

    [Fact]
    public async Task HandleAsync_BranchScoped_ReturnsBranchsEffectiveSettings()
    {
        const int guildBranchId = 7;
        var query = new GetGuildNotificationSettingsQuery { GuildId = GuildId, RequesterDiscordId = RequesterId, GuildBranchId = guildBranchId };
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, guildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _notificationSettings.Setup(r => r.GetEffectiveForGuildAsync(GuildId, guildBranchId, default)).ReturnsAsync(
        [
            new GuildNotificationSetting { GuildId = GuildId, GuildBranchId = guildBranchId, EventType = GuildNotificationEventType.AbsenceAdded, Enabled = true, ChannelId = "chan-branch" },
        ]);

        var result = await _sut.HandleAsync(query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(r => r.EventType == GuildNotificationEventType.AbsenceAdded && r.Enabled && r.ChannelId == "chan-branch" && r.GuildBranchId == guildBranchId);
    }
}
