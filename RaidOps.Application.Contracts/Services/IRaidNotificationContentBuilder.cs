using RaidOps.Domain.Models.Raids;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

namespace RaidOps.Application.Contracts.Services;

/// <summary>
/// Builds the Discord embed content for the two raid notification families ("Raid changes" and
/// "Raid composition changes") — title, color, author (acting officer's guild nickname + avatar)
/// and description, all localized to the guild's configured
/// <see cref="RaidOps.Domain.Models.Discord.Guild.Language"/>. One method per event type, since
/// unlike absences (which vary only by <c>AbsenceKind</c> around a single date-range shape) each
/// raid event carries a structurally different payload (a reschedule needs an old/new time, a swap
/// needs two characters, etc.).
/// </summary>
public interface IRaidNotificationContentBuilder
{
    /// <summary>
    /// Resolves the guild's configured notification language, defaulting to <c>"en"</c> when unset.
    /// </summary>
    Task<string> GetGuildLanguageAsync(string guildId, CancellationToken cancellationToken = default);

    /// <summary>Raid changes family — a raid event was published.</summary>
    Task<DiscordEmbedContent> BuildPublishedAsync(string guildId, string requesterDiscordId, RaidEvent raidEvent, CancellationToken cancellationToken = default);

    /// <summary>Raid changes family — an already-published raid event was deleted.</summary>
    Task<DiscordEmbedContent> BuildCancelledAsync(string guildId, string requesterDiscordId, RaidEvent raidEvent, CancellationToken cancellationToken = default);

    /// <summary>Raid changes family — an already-published raid event's start time was changed.</summary>
    Task<DiscordEmbedContent> BuildRescheduledAsync(string guildId, string requesterDiscordId, RaidEvent raidEvent, DateTime oldStartsAtUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Raid composition changes family — a character was assigned to a slot. <paramref name="character"/>'s
    /// name is shown with a class icon and a spec icon in front of it (see
    /// <see cref="RaidOps.ExternalApplication.Contracts.Services.DiscordBot.WowClassEmojiNames"/>/
    /// <see cref="RaidOps.ExternalApplication.Contracts.Services.DiscordBot.WowSpecEmojiNames"/>) —
    /// either <see cref="RaidCharacterRef.ClassId"/>/<see cref="RaidCharacterRef.SpecName"/> can be
    /// <c>null</c> when unknown, the name is still shown, just without that icon.
    /// </summary>
    Task<DiscordEmbedContent> BuildSlotAssignedAsync(string guildId, string requesterDiscordId, RaidEvent raidEvent, RaidCharacterRef character, SlotCoordinate slot, CancellationToken cancellationToken = default);

    /// <summary>Raid composition changes family — a character was unassigned from a slot.</summary>
    Task<DiscordEmbedContent> BuildSlotUnassignedAsync(string guildId, string requesterDiscordId, RaidEvent raidEvent, RaidCharacterRef character, SlotCoordinate slot, CancellationToken cancellationToken = default);

    /// <summary>Raid composition changes family — two characters' slots were swapped.</summary>
    Task<DiscordEmbedContent> BuildSlotsSwappedAsync(string guildId, string requesterDiscordId, RaidEvent raidEvent, RaidCharacterRef characterA, RaidCharacterRef characterB, CancellationToken cancellationToken = default);

    /// <summary>
    /// Raid composition changes family — a slot assignment's spec was changed. Unlike the other
    /// composition events, <paramref name="character"/> is shown with only its class icon (pass a
    /// <see cref="RaidCharacterRef"/> with <see cref="RaidCharacterRef.SpecName"/> left <c>null</c>)
    /// — the spec is the very thing changing, so it gets its own before/after icons instead.
    /// </summary>
    Task<DiscordEmbedContent> BuildSlotSpecChangedAsync(string guildId, string requesterDiscordId, RaidEvent raidEvent, RaidCharacterRef character, string oldSpecName, string newSpecName, CancellationToken cancellationToken = default);
}

/// <summary>
/// A character reference for a raid composition-change notification — enough to render a
/// <c>"{class icon}{spec icon} **{name}**"</c> label. <see cref="ClassId"/>/<see cref="SpecName"/>
/// are optional: either missing (or an icon simply not synced on the bot yet) just drops that one
/// icon rather than failing the notification.
/// </summary>
/// <param name="Name">Character name.</param>
/// <param name="ClassId">Blizzard class ID, resolves the class icon.</param>
/// <param name="SpecName">Spec display name, resolves the spec icon — leave <c>null</c> to omit it (e.g. <see cref="IRaidNotificationContentBuilder.BuildSlotSpecChangedAsync"/>'s character mention).</param>
public readonly record struct RaidCharacterRef(string Name, int? ClassId, string? SpecName = null);

/// <summary>A (group, slot) coordinate within a raid event's grid.</summary>
public readonly record struct SlotCoordinate(int GroupNumber, int SlotNumber);
