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

public class RaidEventUpdateNotifierTests
{
    private readonly Mock<IRaidEventRepository> _raidEventRepository = new();
    private readonly Mock<IGuildNotificationDispatcher> _guildNotificationDispatcher = new();
    private readonly Mock<IRaidNotificationContentBuilder> _raidNotificationContentBuilder = new();
    private readonly Mock<IRaidSignupAnnouncementService> _raidSignupAnnouncementService = new();
    private readonly Mock<IRaidCompositionAnnouncementService> _raidCompositionAnnouncementService = new();
    private readonly Mock<IDiscordBotService> _discordBotService = new();
    private readonly Mock<IGuildService> _guildService = new();
    private readonly Mock<ILogger<RaidEventUpdateNotifier>> _logger = new();
    private readonly RaidEventUpdateNotifier _sut;

    private const string GuildId = "guild-1";
    private const string RequesterId = "officer-1";
    private const int GuildBranchId = 10;
    private const int EventId = 5;

    public RaidEventUpdateNotifierTests()
    {
        _discordBotService.Setup(d => d.Guilds).Returns(_guildService.Object);
        _sut = new RaidEventUpdateNotifier(
            _raidEventRepository.Object, _guildNotificationDispatcher.Object, _raidNotificationContentBuilder.Object,
            _raidSignupAnnouncementService.Object, _raidCompositionAnnouncementService.Object, _discordBotService.Object, _logger.Object);
    }

    // ── MoveDedicatedChannelAsync ─────────────────────────────────────────────

    [Fact]
    public async Task MoveDedicatedChannelAsync_DropsOldEmbedsAndClearsReferences()
    {
        var existing = new RaidEvent { Id = EventId, DedicatedAnnouncementChannelId = "111", DedicatedAnnouncementChannelIsBotOwned = false };

        await _sut.MoveDedicatedChannelAsync(EventId, GuildBranchId, existing);

        _raidSignupAnnouncementService.Verify(s => s.DeleteSignupCallAsync(existing, default), Times.Once);
        _raidCompositionAnnouncementService.Verify(s => s.DeleteAnnouncementAsync(existing, default), Times.Once);
        _raidEventRepository.Verify(r => r.ClearAnnouncementReferencesAsync(EventId, GuildBranchId, default), Times.Once);
    }

    [Fact]
    public async Task MoveDedicatedChannelAsync_OldChannelBotOwned_DeletesOldChannel()
    {
        var existing = new RaidEvent { Id = EventId, DedicatedAnnouncementChannelId = "111", DedicatedAnnouncementChannelIsBotOwned = true };

        await _sut.MoveDedicatedChannelAsync(EventId, GuildBranchId, existing);

        _guildService.Verify(g => g.DeleteChannelAsync("111", default), Times.Once);
    }

    [Fact]
    public async Task MoveDedicatedChannelAsync_OldChannelNotBotOwned_NeverDeletesOldChannel()
    {
        var existing = new RaidEvent { Id = EventId, DedicatedAnnouncementChannelId = "111", DedicatedAnnouncementChannelIsBotOwned = false };

        await _sut.MoveDedicatedChannelAsync(EventId, GuildBranchId, existing);

        _guildService.Verify(g => g.DeleteChannelAsync(It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task MoveDedicatedChannelAsync_BotOwnedButNoChannelId_NeverDeletesOldChannel()
    {
        var existing = new RaidEvent { Id = EventId, DedicatedAnnouncementChannelId = null, DedicatedAnnouncementChannelIsBotOwned = true };

        await _sut.MoveDedicatedChannelAsync(EventId, GuildBranchId, existing);

        _guildService.Verify(g => g.DeleteChannelAsync(It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task MoveDedicatedChannelAsync_OldChannelDeleteThrows_StillSucceeds()
    {
        var existing = new RaidEvent { Id = EventId, DedicatedAnnouncementChannelId = "111", DedicatedAnnouncementChannelIsBotOwned = true };
        _guildService.Setup(g => g.DeleteChannelAsync("111", default)).ThrowsAsync(new InvalidOperationException("gone"));

        var act = () => _sut.MoveDedicatedChannelAsync(EventId, GuildBranchId, existing);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task MoveDedicatedChannelAsync_SignupMode_RepostsSignupCallInNewChannel()
    {
        var existing = new RaidEvent { Id = EventId, SignupMode = SignupMode.Signup, DedicatedAnnouncementChannelId = "111" };
        var refreshed = new RaidEvent { Id = EventId, SignupMode = SignupMode.Signup, DedicatedAnnouncementChannelId = "222" };
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(refreshed);

        await _sut.MoveDedicatedChannelAsync(EventId, GuildBranchId, existing);

        _raidSignupAnnouncementService.Verify(s => s.PublishOrUpdateSignupCallAsync(refreshed, default), Times.Once);
    }

    [Fact]
    public async Task MoveDedicatedChannelAsync_NotSignupMode_NeverRepostsSignupCall()
    {
        var existing = new RaidEvent { Id = EventId, SignupMode = SignupMode.DefaultPresent, DedicatedAnnouncementChannelId = "111" };

        await _sut.MoveDedicatedChannelAsync(EventId, GuildBranchId, existing);

        _raidSignupAnnouncementService.Verify(s => s.PublishOrUpdateSignupCallAsync(It.IsAny<RaidEvent>(), default), Times.Never);
        _raidEventRepository.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task MoveDedicatedChannelAsync_SignupModeButEventGoneAfterMove_NeverRepostsSignupCall()
    {
        var existing = new RaidEvent { Id = EventId, SignupMode = SignupMode.Signup, DedicatedAnnouncementChannelId = "111" };
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync((RaidEvent?)null);

        await _sut.MoveDedicatedChannelAsync(EventId, GuildBranchId, existing);

        _raidSignupAnnouncementService.Verify(s => s.PublishOrUpdateSignupCallAsync(It.IsAny<RaidEvent>(), default), Times.Never);
    }

    // ── NotifyRescheduledAsync ────────────────────────────────────────────────

    [Fact]
    public async Task NotifyRescheduledAsync_BuildsAndDispatchesTheRescheduledEmbed()
    {
        var raidEvent = new RaidEvent { Id = EventId, GuildId = GuildId, GuildBranchId = GuildBranchId };
        var oldStartsAtUtc = new DateTime(2026, 1, 1, 20, 0, 0, DateTimeKind.Utc);
        var embed = new DiscordEmbedContent("Raid rescheduled");
        _raidNotificationContentBuilder
            .Setup(b => b.BuildRescheduledAsync(GuildId, RequesterId, raidEvent, oldStartsAtUtc, default))
            .ReturnsAsync(embed);

        await _sut.NotifyRescheduledAsync(GuildId, RequesterId, GuildBranchId, raidEvent, oldStartsAtUtc);

        _guildNotificationDispatcher.Verify(d => d.NotifyAsync(GuildId, GuildNotificationEventType.RaidRescheduled, GuildBranchId, embed, default), Times.Once);
    }
}
