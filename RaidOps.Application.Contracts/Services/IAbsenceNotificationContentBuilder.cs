using RaidOps.Domain.Enums;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

namespace RaidOps.Application.Contracts.Services;

/// <summary>
/// Builds the Discord embed content for the "Absences" notification family — title, color,
/// author (declaring member's guild nickname + avatar) and description, all localized to the
/// guild's configured <see cref="RaidOps.Domain.Models.Discord.Guild.Language"/>. Callers supply
/// only the event-specific fields (e.g. dates, pattern label).
/// </summary>
public interface IAbsenceNotificationContentBuilder
{
    /// <summary>
    /// Resolves the guild's configured notification language, defaulting to <c>"en"</c> when unset.
    /// Exposed separately so callers can localize their own event-specific fields (e.g. a date
    /// range) before assembling the final embed via <see cref="BuildAsync"/>.
    /// </summary>
    Task<string> GetGuildLanguageAsync(string guildId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the full embed for the given event, merging in the caller-supplied fields. The
    /// title/description wording is driven by <paramref name="kind"/> — a full-day absence, a late
    /// arrival, an early leave, and a bounded partial window all read differently, instead of every
    /// Partial declaration being announced as a generic "absence".
    /// </summary>
    Task<DiscordEmbedContent> BuildAsync(
        string guildId,
        string requesterDiscordId,
        GuildNotificationEventType eventType,
        AbsenceKind kind,
        IReadOnlyList<DiscordEmbedField> fields,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the embed for a recurring pattern event (created/removed). Unlike <see cref="BuildAsync"/>
    /// there's no single date range to announce, so the description lists every day the pattern
    /// actually marks (Absent/Partial) — the same day set the audit log's own pattern summary shows —
    /// instead of the meaningless label/cycle-length pair a generic embed would otherwise carry.
    /// </summary>
    Task<DiscordEmbedContent> BuildPatternAsync(
        string guildId,
        string requesterDiscordId,
        GuildNotificationEventType eventType,
        DateOnly anchorDate,
        int cycleLengthDays,
        IReadOnlyList<PatternDayNotification> days,
        CancellationToken cancellationToken = default);
}

/// <summary>One day of a recurring pattern's cycle, as needed to render it in a Discord notification.</summary>
/// <param name="OffsetInCycle">Zero-based offset within the pattern's cycle.</param>
/// <param name="Status">Declared status for this offset — only <see cref="DayAvailabilityStatus.Absent"/> or <see cref="DayAvailabilityStatus.Partial"/> are meaningful here.</param>
/// <param name="Reason">Optional free-text reason shown alongside the day.</param>
/// <param name="AvailableFrom">When <paramref name="Status"/> is <see cref="DayAvailabilityStatus.Partial"/>, the time from which the member becomes available.</param>
/// <param name="AvailableUntil">When <paramref name="Status"/> is <see cref="DayAvailabilityStatus.Partial"/>, the time until which the member remains available.</param>
public record PatternDayNotification(int OffsetInCycle, DayAvailabilityStatus Status, string? Reason, TimeOnly? AvailableFrom, TimeOnly? AvailableUntil);
