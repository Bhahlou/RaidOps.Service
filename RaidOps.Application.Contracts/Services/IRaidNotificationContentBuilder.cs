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

    /// <summary>
    /// Builds the standing "current composition" announcement embed for a published raid event —
    /// one field per group, one line per slot (character name with class/spec icons, or an "empty"
    /// placeholder), grouped and numbered the same way the raid builder grid is. No author — unlike
    /// the other events above, this isn't "posted by X", it's a re-rendered snapshot of the current
    /// state that may reflect several officers' edits since it was first posted.
    /// </summary>
    Task<DiscordEmbedContent> BuildCompositionAnnouncementAsync(string guildId, RaidEvent raidEvent, IReadOnlyList<RaidSlotAssignment> assignments, CancellationToken cancellationToken = default);

    /// <summary>
    /// Composition announcement family (DM) — a player was added to a published raid's
    /// composition, whether via a slot assignment or because they were already assigned when the
    /// raid was published. Names the character/spec they were added with.
    /// </summary>
    /// <param name="isInitialPublish">
    /// True when this DM is sent because the raid itself was just published (the player was
    /// already assigned from the draft phase) — uses a "{raid} published" title instead of the
    /// generic "Added to the raid" used for a slot assignment on an already-published raid.
    /// </param>
    Task<DiscordEmbedContent> BuildPlayerAddedDmAsync(string guildId, RaidEvent raidEvent, RaidCharacterRef character, bool isInitialPublish, CancellationToken cancellationToken = default);

    /// <summary>
    /// Composition announcement family (DM) — a player was removed from a published raid's
    /// composition. Names the character/spec they were removed with.
    /// </summary>
    Task<DiscordEmbedContent> BuildPlayerRemovedDmAsync(string guildId, RaidEvent raidEvent, RaidCharacterRef character, CancellationToken cancellationToken = default);

    /// <summary>
    /// Composition announcement family (DM) — a slot assignment's spec was changed on a published
    /// raid. <paramref name="character"/> is shown without a spec icon (the spec is what's
    /// changing, it gets its own before/after icons instead), same convention as
    /// <see cref="BuildSlotSpecChangedAsync"/>.
    /// </summary>
    Task<DiscordEmbedContent> BuildPlayerSpecChangedDmAsync(string guildId, RaidEvent raidEvent, RaidCharacterRef character, string oldSpecName, string newSpecName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Composition announcement family (DM) — a published raid a player was in got cancelled
    /// (deleted). Unlike the other DMs here, sent unconditionally regardless of the DM setting —
    /// see <see cref="RaidOps.Application.Contracts.Services.IRaidCompositionAnnouncementService.NotifyPlayerRaidCancelledAsync"/>.
    /// </summary>
    Task<DiscordEmbedContent> BuildRaidCancelledDmAsync(string guildId, RaidEvent raidEvent, RaidCharacterRef character, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deep-link to <paramref name="raidEvent"/>'s detail page on the site, for the composition
    /// announcement embed/DMs' title/description. Returns <c>null</c> if the front end's base URL
    /// isn't configured (a link is a nice-to-have, never a reason to fail a notification).
    /// </summary>
    string? BuildRaidEventUrl(RaidEvent raidEvent);
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
