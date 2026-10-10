using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Events.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Events.CommandHandlers;
using RaidOps.Application.Implementations.Raids.Helpers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Raids;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Events.CommandHandlers;

public class TriggerRaidGroupingCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IRaidEventRepository> _raidEventRepository = new();
    private readonly Mock<IRaidCompositionRepository> _raidCompositionRepository = new();
    private readonly Mock<IGuildNotificationSettingsRepository> _notificationSettingsRepository = new();
    private readonly Mock<IRaidNotificationContentBuilder> _contentBuilder = new();
    private readonly Mock<IDiscordBotService> _discordBotService = new();
    private readonly Mock<IMessageService> _messages = new();
    private readonly TriggerRaidGroupingCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "officer-1";
    private const int EventId = 5;
    private const string ChannelId = "999";
    private const string OwnCharacterName = "Arthas";

    public TriggerRaidGroupingCommandHandlerTests()
    {
        _discordBotService.Setup(d => d.Messages).Returns(_messages.Object);
        _sut = new TriggerRaidGroupingCommandHandler(
            _access.Object, _raidEventRepository.Object, _raidCompositionRepository.Object,
            _notificationSettingsRepository.Object, _contentBuilder.Object, _discordBotService.Object);

        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _contentBuilder.Setup(b => b.GetGuildLanguageAsync(GuildId, default)).ReturnsAsync("en");
    }

    private static TriggerRaidGroupingCommand MakeCommand(string? characterName = null) => new()
    {
        GuildId = GuildId,
        GuildBranchId = GuildBranchId,
        EventId = EventId,
        RequesterDiscordId = RequesterId,
        CharacterName = characterName,
    };

    private static RaidEvent MakePublishedEvent() => new()
    {
        Id = EventId,
        GuildId = GuildId,
        GuildBranchId = GuildBranchId,
        Name = "Split 1",
        PublicationStatus = RaidPublicationStatus.Published,
    };

    private static RaidSlotAssignment MakeAssignment(int characterId, string playerDiscordId, string characterName) => new()
    {
        RaidEventId = EventId,
        CharacterId = characterId,
        AssignedPlayerDiscordId = playerDiscordId,
        Character = new Character { Id = characterId, Name = characterName },
    };

    private void SetupPublishedEventWithChannel(RaidEvent raidEvent, bool enabled = true, string? channelId = ChannelId) =>
        SetupPublishedEventWithChannel(raidEvent, new GuildNotificationSetting { GuildId = GuildId, EventType = GuildNotificationEventType.RaidCompositionAnnouncementPosted, Enabled = enabled, ChannelId = channelId });

    private void SetupPublishedEventWithChannel(RaidEvent raidEvent, GuildNotificationSetting? setting)
    {
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(raidEvent);
        _notificationSettingsRepository.Setup(r => r.GetAsync(GuildId, GuildNotificationEventType.RaidCompositionAnnouncementPosted, GuildBranchId, default)).ReturnsAsync(setting);
    }

    // ── Access / existence / publication gating ──────────────────────────────

    [Fact]
    public async Task HandleAsync_NotOfficer_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
        _raidEventRepository.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_EventNotFound_ReturnsRaidEventNotFound()
    {
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync((RaidEvent?)null);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidEventNotFound);
    }

    [Fact]
    public async Task HandleAsync_EventIsDraft_ReturnsRaidEventNotPublished()
    {
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent { Id = EventId, PublicationStatus = RaidPublicationStatus.Draft });

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidEventNotPublished);
    }

    // ── Announcement channel resolution ───────────────────────────────────────

    [Fact]
    public async Task HandleAsync_NoNotificationSettingRow_ReturnsNoAnnouncementChannelConfigured()
    {
        SetupPublishedEventWithChannel(MakePublishedEvent(), setting: null);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.NoAnnouncementChannelConfigured);
    }

    [Fact]
    public async Task HandleAsync_SettingDisabled_ReturnsNoAnnouncementChannelConfigured()
    {
        SetupPublishedEventWithChannel(MakePublishedEvent(), enabled: false);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.NoAnnouncementChannelConfigured);
    }

    [Fact]
    public async Task HandleAsync_SettingEnabledButNoChannelId_ReturnsNoAnnouncementChannelConfigured()
    {
        SetupPublishedEventWithChannel(MakePublishedEvent(), channelId: null);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.NoAnnouncementChannelConfigured);
    }

    // ── Assignments ────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_NoAssignments_ReturnsNoAssignmentsToNotify()
    {
        SetupPublishedEventWithChannel(MakePublishedEvent());
        _raidCompositionRepository.Setup(r => r.GetAssignmentsForEventAsync(EventId, default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.NoAssignmentsToNotify);
    }

    // ── Character resolution ──────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_CharacterNameProvidedButNotAssigned_ReturnsRaidGroupingCharacterNotFound()
    {
        SetupPublishedEventWithChannel(MakePublishedEvent());
        _raidCompositionRepository.Setup(r => r.GetAssignmentsForEventAsync(EventId, default))
            .ReturnsAsync([MakeAssignment(1, "other-player", "Jaina")]);

        var result = await _sut.HandleAsync(MakeCommand(characterName: "Sylvanas"));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidGroupingCharacterNotFound);
        _messages.Verify(m => m.SendMessageWithEmbedAsync(It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<DiscordEmbedContent>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_CharacterNameProvided_MatchesCaseInsensitively()
    {
        SetupPublishedEventWithChannel(MakePublishedEvent());
        _raidCompositionRepository.Setup(r => r.GetAssignmentsForEventAsync(EventId, default))
            .ReturnsAsync([MakeAssignment(1, "other-player", "Jaina")]);
        _contentBuilder.Setup(b => b.BuildCompositionAnnouncementAsync(GuildId, It.IsAny<RaidEvent>(), It.IsAny<IReadOnlyList<RaidSlotAssignment>>(), default))
            .ReturnsAsync(new DiscordEmbedContent("Split 1"));

        var result = await _sut.HandleAsync(MakeCommand(characterName: "jAINA"));

        result.IsSuccess.Should().BeTrue();
        _messages.Verify(m => m.SendMessageWithEmbedAsync(
            ulong.Parse(ChannelId), It.Is<string>(s => s.Contains("Jaina")), It.IsAny<DiscordEmbedContent>(), default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_NoCharacterNameProvided_RequesterHasNoAssignedCharacter_ReturnsRaidGroupingRequesterHasNoCharacter()
    {
        SetupPublishedEventWithChannel(MakePublishedEvent());
        _raidCompositionRepository.Setup(r => r.GetAssignmentsForEventAsync(EventId, default))
            .ReturnsAsync([MakeAssignment(1, "other-player", "Jaina")]);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidGroupingRequesterHasNoCharacter);
        _messages.Verify(m => m.SendMessageWithEmbedAsync(It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<DiscordEmbedContent>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_NoCharacterNameProvided_RequesterHasAssignedCharacter_UsesOwnCharacter()
    {
        SetupPublishedEventWithChannel(MakePublishedEvent());
        _raidCompositionRepository.Setup(r => r.GetAssignmentsForEventAsync(EventId, default))
            .ReturnsAsync([MakeAssignment(1, RequesterId, OwnCharacterName)]);
        _contentBuilder.Setup(b => b.BuildCompositionAnnouncementAsync(GuildId, It.IsAny<RaidEvent>(), It.IsAny<IReadOnlyList<RaidSlotAssignment>>(), default))
            .ReturnsAsync(new DiscordEmbedContent("Split 1"));

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _messages.Verify(m => m.SendMessageWithEmbedAsync(
            It.IsAny<ulong>(), It.Is<string>(s => s.Contains(OwnCharacterName)), It.IsAny<DiscordEmbedContent>(), default), Times.Once);
    }

    // ── Success — message content ─────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_Success_SendsMessageToConfiguredChannelWithCompositionEmbed()
    {
        var raidEvent = MakePublishedEvent();
        SetupPublishedEventWithChannel(raidEvent);
        _raidCompositionRepository.Setup(r => r.GetAssignmentsForEventAsync(EventId, default))
            .ReturnsAsync([MakeAssignment(1, RequesterId, OwnCharacterName)]);
        var embed = new DiscordEmbedContent("Split 1");
        _contentBuilder.Setup(b => b.BuildCompositionAnnouncementAsync(GuildId, raidEvent, It.IsAny<IReadOnlyList<RaidSlotAssignment>>(), default))
            .ReturnsAsync(embed);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _messages.Verify(m => m.SendMessageWithEmbedAsync(ulong.Parse(ChannelId), It.IsAny<string>(), embed, default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Success_MessageMentionsEveryDistinctAssignedPlayer()
    {
        SetupPublishedEventWithChannel(MakePublishedEvent());
        _raidCompositionRepository.Setup(r => r.GetAssignmentsForEventAsync(EventId, default))
            .ReturnsAsync(
            [
                MakeAssignment(1, RequesterId, OwnCharacterName),
                MakeAssignment(2, "player-2", "Jaina"),
                // A second character owned by the same player as an existing assignment (e.g. a
                // multi-boxer) must not produce a duplicate mention.
                MakeAssignment(3, "player-2", "Jaina-Alt"),
            ]);
        _contentBuilder.Setup(b => b.BuildCompositionAnnouncementAsync(GuildId, It.IsAny<RaidEvent>(), It.IsAny<IReadOnlyList<RaidSlotAssignment>>(), default))
            .ReturnsAsync(new DiscordEmbedContent("Split 1"));
        string? sentMessage = null;
        _messages.Setup(m => m.SendMessageWithEmbedAsync(It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<DiscordEmbedContent>(), default))
            .Callback<ulong, string, DiscordEmbedContent, CancellationToken>((_, message, _, _) => sentMessage = message);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        sentMessage.Should().Contain($"<@{RequesterId}>").And.Contain("<@player-2>");
        // Distinct: player-2 owns two of the three assignments but should only be mentioned once.
        sentMessage!.Split("<@player-2>").Should().HaveCount(2);
    }

    [Fact]
    public async Task HandleAsync_Success_UsesGuildLanguageForMessageText()
    {
        SetupPublishedEventWithChannel(MakePublishedEvent());
        _raidCompositionRepository.Setup(r => r.GetAssignmentsForEventAsync(EventId, default))
            .ReturnsAsync([MakeAssignment(1, RequesterId, OwnCharacterName)]);
        _contentBuilder.Setup(b => b.GetGuildLanguageAsync(GuildId, default)).ReturnsAsync("fr");
        _contentBuilder.Setup(b => b.BuildCompositionAnnouncementAsync(GuildId, It.IsAny<RaidEvent>(), It.IsAny<IReadOnlyList<RaidSlotAssignment>>(), default))
            .ReturnsAsync(new DiscordEmbedContent("Split 1"));
        string? sentMessage = null;
        _messages.Setup(m => m.SendMessageWithEmbedAsync(It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<DiscordEmbedContent>(), default))
            .Callback<ulong, string, DiscordEmbedContent, CancellationToken>((_, message, _, _) => sentMessage = message);

        await _sut.HandleAsync(MakeCommand());

        sentMessage.Should().Be(RaidNotificationText.GetGroupingPingMessage($"<@{RequesterId}>", "Split 1", OwnCharacterName, "fr"));
    }
}
