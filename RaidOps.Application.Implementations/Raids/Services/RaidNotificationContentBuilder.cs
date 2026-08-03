using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Notifications.Helpers;
using RaidOps.Application.Implementations.Raids.Helpers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Raids;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Services;

/// <inheritdoc cref="IRaidNotificationContentBuilder"/>
public class RaidNotificationContentBuilder(
    IGuildsRepository guildsRepository,
    IDiscordBotService discordBotService) : IRaidNotificationContentBuilder
{
    /// <inheritdoc/>
    public async Task<string> GetGuildLanguageAsync(string guildId, CancellationToken cancellationToken = default)
    {
        var guild = await guildsRepository.GetByIdAsync(guildId, cancellationToken);
        return guild?.Language ?? "en";
    }

    /// <inheritdoc/>
    public async Task<DiscordEmbedContent> BuildPublishedAsync(string guildId, string requesterDiscordId, RaidEvent raidEvent, CancellationToken cancellationToken = default)
    {
        var (guild, language) = await ResolveGuildAsync(guildId, cancellationToken);
        var description = RaidNotificationText.GetPublishedDescription(requesterDiscordId, raidEvent.Name, language);
        var startsAt = RaidNotificationText.FormatDateTime(GuildTimeHelper.ToGuildLocalDateTime(raidEvent.StartsAtUtc, guild?.Timezone), language);

        return BuildEmbed(GuildNotificationEventType.RaidPublished, language, description, guildId, requesterDiscordId,
            [new DiscordEmbedField("Starts", startsAt)], cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<DiscordEmbedContent> BuildCancelledAsync(string guildId, string requesterDiscordId, RaidEvent raidEvent, CancellationToken cancellationToken = default)
    {
        var (_, language) = await ResolveGuildAsync(guildId, cancellationToken);
        var description = RaidNotificationText.GetCancelledDescription(requesterDiscordId, raidEvent.Name, language);

        return BuildEmbed(GuildNotificationEventType.RaidCancelled, language, description, guildId, requesterDiscordId, null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<DiscordEmbedContent> BuildRescheduledAsync(string guildId, string requesterDiscordId, RaidEvent raidEvent, DateTime oldStartsAtUtc, CancellationToken cancellationToken = default)
    {
        var (guild, language) = await ResolveGuildAsync(guildId, cancellationToken);
        var oldTime = RaidNotificationText.FormatDateTime(GuildTimeHelper.ToGuildLocalDateTime(oldStartsAtUtc, guild?.Timezone), language);
        var newTime = RaidNotificationText.FormatDateTime(GuildTimeHelper.ToGuildLocalDateTime(raidEvent.StartsAtUtc, guild?.Timezone), language);
        var description = RaidNotificationText.GetRescheduledDescription(requesterDiscordId, raidEvent.Name, oldTime, newTime, language);

        return BuildEmbed(GuildNotificationEventType.RaidRescheduled, language, description, guildId, requesterDiscordId, null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<DiscordEmbedContent> BuildSlotAssignedAsync(string guildId, string requesterDiscordId, RaidEvent raidEvent, RaidCharacterRef character, SlotCoordinate slot, CancellationToken cancellationToken = default)
    {
        var (_, language) = await ResolveGuildAsync(guildId, cancellationToken);
        var description = RaidNotificationText.GetSlotAssignedDescription(requesterDiscordId, raidEvent.Name, CharacterLabel(character), slot.GroupNumber, slot.SlotNumber, language);

        return BuildEmbed(GuildNotificationEventType.RaidSlotAssigned, language, description, guildId, requesterDiscordId, null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<DiscordEmbedContent> BuildSlotUnassignedAsync(string guildId, string requesterDiscordId, RaidEvent raidEvent, RaidCharacterRef character, SlotCoordinate slot, CancellationToken cancellationToken = default)
    {
        var (_, language) = await ResolveGuildAsync(guildId, cancellationToken);
        var description = RaidNotificationText.GetSlotUnassignedDescription(requesterDiscordId, raidEvent.Name, CharacterLabel(character), slot.GroupNumber, slot.SlotNumber, language);

        return BuildEmbed(GuildNotificationEventType.RaidSlotUnassigned, language, description, guildId, requesterDiscordId, null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<DiscordEmbedContent> BuildSlotsSwappedAsync(string guildId, string requesterDiscordId, RaidEvent raidEvent, RaidCharacterRef characterA, RaidCharacterRef characterB, CancellationToken cancellationToken = default)
    {
        var (_, language) = await ResolveGuildAsync(guildId, cancellationToken);
        var description = RaidNotificationText.GetSlotsSwappedDescription(
            requesterDiscordId, raidEvent.Name, CharacterLabel(characterA), CharacterLabel(characterB), language);

        return BuildEmbed(GuildNotificationEventType.RaidSlotsSwapped, language, description, guildId, requesterDiscordId, null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<DiscordEmbedContent> BuildSlotSpecChangedAsync(string guildId, string requesterDiscordId, RaidEvent raidEvent, RaidCharacterRef character, string oldSpecName, string newSpecName, CancellationToken cancellationToken = default)
    {
        var (_, language) = await ResolveGuildAsync(guildId, cancellationToken);
        var description = RaidNotificationText.GetSlotSpecChangedDescription(
            requesterDiscordId, raidEvent.Name, CharacterLabel(character), SpecLabel(oldSpecName, character.ClassId), SpecLabel(newSpecName, character.ClassId), language);

        return BuildEmbed(GuildNotificationEventType.RaidSlotSpecChanged, language, description, guildId, requesterDiscordId, null, cancellationToken);
    }

    /// <summary>
    /// <c>"{class emoji}{spec emoji} **{name}**"</c> — either icon is silently dropped (never the
    /// whole label) when its class/spec is unknown or its emoji hasn't synced on this bot yet
    /// (<see cref="IEmojiService.GetMarkdown"/> returns <c>null</c> rather than throwing — an icon
    /// is a nice-to-have, never a reason to fail the notification). <see cref="RaidCharacterRef.SpecName"/>
    /// is left <c>null</c> by <see cref="BuildSlotSpecChangedAsync"/>, which shows the spec's own
    /// before/after icons instead of one next to the character.
    /// </summary>
    private string CharacterLabel(RaidCharacterRef character)
    {
        var classEmoji = character.ClassId is { } classId && WowClassEmojiNames.ByClassId.TryGetValue(classId, out var className)
            ? discordBotService.Emojis.GetMarkdown(className)
            : null;
        var specEmoji = character.ClassId is { } cid && character.SpecName is { } specName
            ? discordBotService.Emojis.GetMarkdown(WowSpecEmojiNames.GetName(cid, specName))
            : null;

        var icons = $"{classEmoji}{specEmoji}";
        return icons.Length == 0 ? $"**{character.Name}**" : $"{icons} **{character.Name}**";
    }

    /// <summary><c>"{spec emoji} **{specName}**"</c>, or just <c>"**{specName}**"</c> when the icon isn't resolvable — same fallback rationale as <see cref="CharacterLabel"/>.</summary>
    private string SpecLabel(string specName, int? characterClassId)
    {
        var emoji = characterClassId is { } classId
            ? discordBotService.Emojis.GetMarkdown(WowSpecEmojiNames.GetName(classId, specName))
            : null;

        return emoji is null ? $"**{specName}**" : $"{emoji} **{specName}**";
    }

    private async Task<(Guild? Guild, string Language)> ResolveGuildAsync(string guildId, CancellationToken cancellationToken)
    {
        var guild = await guildsRepository.GetByIdAsync(guildId, cancellationToken);
        return (guild, guild?.Language ?? "en");
    }

    private DiscordEmbedContent BuildEmbed(
        GuildNotificationEventType eventType,
        string language,
        string description,
        string guildId,
        string requesterDiscordId,
        IReadOnlyList<DiscordEmbedField>? fields,
        CancellationToken cancellationToken)
    {
        var (title, color) = RaidNotificationText.GetTitleAndColor(eventType, language);
        var author = DiscordEmbedAuthorResolver.Resolve(discordBotService, guildId, requesterDiscordId, cancellationToken);

        return new DiscordEmbedContent(
            Title: title,
            Description: description,
            ColorHex: color,
            Fields: fields,
            Author: author);
    }
}
