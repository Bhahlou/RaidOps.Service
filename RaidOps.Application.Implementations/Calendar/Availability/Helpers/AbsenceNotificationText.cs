using System.Globalization;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;

namespace RaidOps.Application.Implementations.Calendar.Availability.Helpers;

/// <summary>
/// Localized title/color/description templates for the "Absences" Discord notification family,
/// keyed by (<see cref="GuildNotificationEventType"/>, <see cref="AbsenceKind"/>, guild language).
/// Falls back to English for an unsupported/missing language rather than throwing — a notification
/// should never fail to send just because <see cref="RaidOps.Domain.Models.Discord.Guild.Language"/>
/// is unset.
/// </summary>
internal static class AbsenceNotificationText
{
    private static readonly Dictionary<(GuildNotificationEventType EventType, AbsenceKind Kind, string Language), (string Title, int Color)> TitleAndColor = new()
    {
        // Discord brand yellow/green — reads clearly against both the light and dark client themes.
        [(GuildNotificationEventType.AbsenceAdded, AbsenceKind.FullDay, "en")] = ("New absence", 0xFEE75C),
        [(GuildNotificationEventType.AbsenceAdded, AbsenceKind.FullDay, "fr")] = ("Nouvelle absence", 0xFEE75C),
        [(GuildNotificationEventType.AbsenceAdded, AbsenceKind.FullDay, "de")] = ("Neue Abwesenheit", 0xFEE75C),
        [(GuildNotificationEventType.AbsenceAdded, AbsenceKind.LateArrival, "en")] = ("Late arrival added", 0xFEE75C),
        [(GuildNotificationEventType.AbsenceAdded, AbsenceKind.LateArrival, "fr")] = ("Retard ajouté", 0xFEE75C),
        [(GuildNotificationEventType.AbsenceAdded, AbsenceKind.LateArrival, "de")] = ("Verspätung hinzugefügt", 0xFEE75C),
        [(GuildNotificationEventType.AbsenceAdded, AbsenceKind.EarlyLeave, "en")] = ("Early leave added", 0xFEE75C),
        [(GuildNotificationEventType.AbsenceAdded, AbsenceKind.EarlyLeave, "fr")] = ("Départ anticipé ajouté", 0xFEE75C),
        [(GuildNotificationEventType.AbsenceAdded, AbsenceKind.EarlyLeave, "de")] = ("Frühzeitiges Verlassen hinzugefügt", 0xFEE75C),
        [(GuildNotificationEventType.AbsenceAdded, AbsenceKind.PartialWindow, "en")] = ("Partial availability added", 0xFEE75C),
        [(GuildNotificationEventType.AbsenceAdded, AbsenceKind.PartialWindow, "fr")] = ("Disponibilité partielle ajoutée", 0xFEE75C),
        [(GuildNotificationEventType.AbsenceAdded, AbsenceKind.PartialWindow, "de")] = ("Teilweise Verfügbarkeit hinzugefügt", 0xFEE75C),

        [(GuildNotificationEventType.AbsenceRemoved, AbsenceKind.FullDay, "en")] = ("Absence removed", 0x57F287),
        [(GuildNotificationEventType.AbsenceRemoved, AbsenceKind.FullDay, "fr")] = ("Absence supprimée", 0x57F287),
        [(GuildNotificationEventType.AbsenceRemoved, AbsenceKind.FullDay, "de")] = ("Abwesenheit entfernt", 0x57F287),
        [(GuildNotificationEventType.AbsenceRemoved, AbsenceKind.LateArrival, "en")] = ("Late arrival removed", 0x57F287),
        [(GuildNotificationEventType.AbsenceRemoved, AbsenceKind.LateArrival, "fr")] = ("Retard supprimé", 0x57F287),
        [(GuildNotificationEventType.AbsenceRemoved, AbsenceKind.LateArrival, "de")] = ("Verspätung entfernt", 0x57F287),
        [(GuildNotificationEventType.AbsenceRemoved, AbsenceKind.EarlyLeave, "en")] = ("Early leave removed", 0x57F287),
        [(GuildNotificationEventType.AbsenceRemoved, AbsenceKind.EarlyLeave, "fr")] = ("Départ anticipé supprimé", 0x57F287),
        [(GuildNotificationEventType.AbsenceRemoved, AbsenceKind.EarlyLeave, "de")] = ("Frühzeitiges Verlassen entfernt", 0x57F287),
        [(GuildNotificationEventType.AbsenceRemoved, AbsenceKind.PartialWindow, "en")] = ("Partial availability removed", 0x57F287),
        [(GuildNotificationEventType.AbsenceRemoved, AbsenceKind.PartialWindow, "fr")] = ("Disponibilité partielle supprimée", 0x57F287),
        [(GuildNotificationEventType.AbsenceRemoved, AbsenceKind.PartialWindow, "de")] = ("Teilweise Verfügbarkeit entfernt", 0x57F287),

        [(GuildNotificationEventType.AbsenceAdded, AbsenceKind.RecurringPattern, "en")] = ("New recurring absences", 0xFEE75C),
        [(GuildNotificationEventType.AbsenceAdded, AbsenceKind.RecurringPattern, "fr")] = ("Nouvelles absences récurrentes", 0xFEE75C),
        [(GuildNotificationEventType.AbsenceAdded, AbsenceKind.RecurringPattern, "de")] = ("Neue wiederkehrende Abwesenheiten", 0xFEE75C),
        [(GuildNotificationEventType.AbsenceRemoved, AbsenceKind.RecurringPattern, "en")] = ("Recurring absences removed", 0x57F287),
        [(GuildNotificationEventType.AbsenceRemoved, AbsenceKind.RecurringPattern, "fr")] = ("Récurrence d'absences supprimée", 0x57F287),
        [(GuildNotificationEventType.AbsenceRemoved, AbsenceKind.RecurringPattern, "de")] = ("Wiederkehrende Abwesenheiten entfernt", 0x57F287),
    };

    private static readonly Dictionary<(GuildNotificationEventType EventType, AbsenceKind Kind, string Language), string> DescriptionTemplate = new()
    {
        [(GuildNotificationEventType.AbsenceAdded, AbsenceKind.FullDay, "en")] = "{0} added a new absence.",
        [(GuildNotificationEventType.AbsenceAdded, AbsenceKind.FullDay, "fr")] = "{0} a ajouté une nouvelle absence.",
        [(GuildNotificationEventType.AbsenceAdded, AbsenceKind.FullDay, "de")] = "{0} hat eine neue Abwesenheit eingetragen.",
        [(GuildNotificationEventType.AbsenceAdded, AbsenceKind.LateArrival, "en")] = "{0} added a late arrival.",
        [(GuildNotificationEventType.AbsenceAdded, AbsenceKind.LateArrival, "fr")] = "{0} a ajouté un retard.",
        [(GuildNotificationEventType.AbsenceAdded, AbsenceKind.LateArrival, "de")] = "{0} hat eine Verspätung eingetragen.",
        [(GuildNotificationEventType.AbsenceAdded, AbsenceKind.EarlyLeave, "en")] = "{0} added an early leave.",
        [(GuildNotificationEventType.AbsenceAdded, AbsenceKind.EarlyLeave, "fr")] = "{0} a ajouté un départ anticipé.",
        [(GuildNotificationEventType.AbsenceAdded, AbsenceKind.EarlyLeave, "de")] = "{0} hat ein frühzeitiges Verlassen eingetragen.",
        [(GuildNotificationEventType.AbsenceAdded, AbsenceKind.PartialWindow, "en")] = "{0} added a partial availability.",
        [(GuildNotificationEventType.AbsenceAdded, AbsenceKind.PartialWindow, "fr")] = "{0} a ajouté une disponibilité partielle.",
        [(GuildNotificationEventType.AbsenceAdded, AbsenceKind.PartialWindow, "de")] = "{0} hat eine teilweise Verfügbarkeit eingetragen.",

        [(GuildNotificationEventType.AbsenceRemoved, AbsenceKind.FullDay, "en")] = "{0} added a new availability.",
        [(GuildNotificationEventType.AbsenceRemoved, AbsenceKind.FullDay, "fr")] = "{0} a ajouté une nouvelle disponibilité.",
        [(GuildNotificationEventType.AbsenceRemoved, AbsenceKind.FullDay, "de")] = "{0} hat eine neue Verfügbarkeit eingetragen.",
        [(GuildNotificationEventType.AbsenceRemoved, AbsenceKind.LateArrival, "en")] = "{0} cancelled a late arrival.",
        [(GuildNotificationEventType.AbsenceRemoved, AbsenceKind.LateArrival, "fr")] = "{0} a annulé un retard.",
        [(GuildNotificationEventType.AbsenceRemoved, AbsenceKind.LateArrival, "de")] = "{0} hat eine Verspätung storniert.",
        [(GuildNotificationEventType.AbsenceRemoved, AbsenceKind.EarlyLeave, "en")] = "{0} cancelled an early leave.",
        [(GuildNotificationEventType.AbsenceRemoved, AbsenceKind.EarlyLeave, "fr")] = "{0} a annulé un départ anticipé.",
        [(GuildNotificationEventType.AbsenceRemoved, AbsenceKind.EarlyLeave, "de")] = "{0} hat ein frühzeitiges Verlassen storniert.",
        [(GuildNotificationEventType.AbsenceRemoved, AbsenceKind.PartialWindow, "en")] = "{0} cancelled a partial availability.",
        [(GuildNotificationEventType.AbsenceRemoved, AbsenceKind.PartialWindow, "fr")] = "{0} a annulé une disponibilité partielle.",
        [(GuildNotificationEventType.AbsenceRemoved, AbsenceKind.PartialWindow, "de")] = "{0} hat eine teilweise Verfügbarkeit storniert.",

        [(GuildNotificationEventType.AbsenceAdded, AbsenceKind.RecurringPattern, "en")] = "{0} added a new recurring absence pattern:",
        [(GuildNotificationEventType.AbsenceAdded, AbsenceKind.RecurringPattern, "fr")] = "{0} a ajouté une nouvelle récurrence d'absences :",
        [(GuildNotificationEventType.AbsenceAdded, AbsenceKind.RecurringPattern, "de")] = "{0} hat ein neues wiederkehrendes Abwesenheitsmuster hinzugefügt:",
        [(GuildNotificationEventType.AbsenceRemoved, AbsenceKind.RecurringPattern, "en")] = "{0} removed a recurring absence pattern:",
        [(GuildNotificationEventType.AbsenceRemoved, AbsenceKind.RecurringPattern, "fr")] = "{0} a supprimé une récurrence d'absences :",
        [(GuildNotificationEventType.AbsenceRemoved, AbsenceKind.RecurringPattern, "de")] = "{0} hat ein wiederkehrendes Abwesenheitsmuster entfernt:",
    };

    /// <summary>
    /// Derives which wording applies from the raw resolved status/bounds — mirrors the front's own
    /// `describePartialTime` split (late/earlyLeave/window) so both surfaces read the same event
    /// the same way.
    /// </summary>
    public static AbsenceKind DetermineKind(DayAvailabilityStatus status, TimeOnly? availableFrom, TimeOnly? availableUntil)
    {
        if (status != DayAvailabilityStatus.Partial)
            return AbsenceKind.FullDay;

        if (availableFrom is not null && availableUntil is null)
            return AbsenceKind.LateArrival;

        if (availableUntil is not null && availableFrom is null)
            return AbsenceKind.EarlyLeave;

        return AbsenceKind.PartialWindow;
    }

    public static (string Title, int Color) GetTitleAndColor(GuildNotificationEventType eventType, AbsenceKind kind, string language)
        => TitleAndColor.TryGetValue((eventType, kind, language), out var value) ? value : TitleAndColor[(eventType, kind, "en")];

    public static string GetDescription(GuildNotificationEventType eventType, AbsenceKind kind, string language, string requesterDiscordId)
    {
        var template = DescriptionTemplate.TryGetValue((eventType, kind, language), out var value) ? value : DescriptionTemplate[(eventType, kind, "en")];
        return string.Format(template, $"<@{requesterDiscordId}>");
    }

    /// <summary>
    /// Builds a recurring pattern notification's description: the intro sentence followed by a
    /// blank line and one bullet per day the pattern actually marks, e.g. "- Mardi : Absent" or
    /// "- Mercredi : dès 21h00" — mirrors the audit log's own day-by-day pattern summary so both
    /// surfaces read the same event the same way, instead of the meaningless label/cycle-length
    /// pair the generic embed used to carry.
    /// </summary>
    public static string GetPatternDescription(
        GuildNotificationEventType eventType,
        string language,
        string requesterDiscordId,
        DateOnly anchorDate,
        int cycleLengthDays,
        IReadOnlyList<PatternDayNotification> days)
    {
        var template = DescriptionTemplate.TryGetValue((eventType, AbsenceKind.RecurringPattern, language), out var value)
            ? value
            : DescriptionTemplate[(eventType, AbsenceKind.RecurringPattern, "en")];
        var sentence = string.Format(template, $"<@{requesterDiscordId}>");

        // A weekly cycle is unambiguous once each day is named ("Mardi"/"Tuesday"), but "Jour 1"/"Day 1"
        // says nothing about how long the cycle actually is without this — an officer reading "Jour 2 :
        // Absent" has no way to know if that's every other day or every 10th day.
        var isWeekly = cycleLengthDays == 7;
        if (!isWeekly)
            sentence += $" {CycleLengthSuffix(cycleLengthDays, language)}";

        var lines = days
            .OrderBy(d => d.OffsetInCycle)
            .Select(d =>
            {
                var dayLabel = isWeekly ? WeekdayLabel(anchorDate, d.OffsetInCycle, language) : DayNumberLabel(d.OffsetInCycle, language);
                return FormatPatternDayLine(dayLabel, d.Status, d.AvailableFrom, d.AvailableUntil, d.Reason, language);
            });

        return $"{sentence}\n\n{string.Join("\n", lines)}";
    }

    private static string FormatPatternDayLine(string dayLabel, DayAvailabilityStatus status, TimeOnly? availableFrom, TimeOnly? availableUntil, string? reason, string language)
    {
        var kind = DetermineKind(status, availableFrom, availableUntil);
        // Non-FullDay kinds are exactly LateArrival/EarlyLeave/PartialWindow, all handled by
        // FormatPartialSuffix's switch, so it never returns null here.
        var statusLabel = kind == AbsenceKind.FullDay ? AbsentWord(language) : FormatPartialSuffix(kind, availableFrom, availableUntil, language)!;
        var line = $"- {dayLabel} : {statusLabel}";
        return string.IsNullOrWhiteSpace(reason) ? line : $"{line} ({reason})";
    }

    private static string AbsentWord(string language) => language == "de" ? "Abwesend" : "Absent";

    private static string CycleLengthSuffix(int cycleLengthDays, string language) => language switch
    {
        "fr" => $"(cycle de {cycleLengthDays} jours)",
        "de" => $"(Zyklus von {cycleLengthDays} Tagen)",
        _ => $"(cycle of {cycleLengthDays} days)",
    };

    /// <summary>Full localized weekday name (e.g. "Mardi") for the day <paramref name="offsetInCycle"/> days after <paramref name="anchorDate"/> — only meaningful for a 7-day cycle.</summary>
    private static string WeekdayLabel(DateOnly anchorDate, int offsetInCycle, string language)
    {
        var culture = GetCulture(language);
        var name = anchorDate.AddDays(offsetInCycle).ToDateTime(TimeOnly.MinValue).ToString("dddd", culture);
        return culture.TextInfo.ToTitleCase(name);
    }

    private static string DayNumberLabel(int offsetInCycle, string language) => language switch
    {
        "fr" => $"Jour {offsetInCycle + 1}",
        "de" => $"Tag {offsetInCycle + 1}",
        _ => $"Day {offsetInCycle + 1}",
    };

    /// <summary>Formats a date (or date range) using the short date pattern of the guild's configured language.</summary>
    public static string FormatDateRange(DateOnly start, DateOnly end, string language)
    {
        var culture = GetCulture(language);
        return start == end
            ? start.ToString("d", culture)
            : $"{start.ToString("d", culture)} → {end.ToString("d", culture)}";
    }

    /// <summary>
    /// Formats the time-bound suffix for a Partial segment (e.g. "from 21:30", "until 17:00",
    /// "09:00 – 17:00"), or <c>null</c> for a <see cref="AbsenceKind.FullDay"/> segment that has
    /// nothing to add.
    /// </summary>
    public static string? FormatPartialSuffix(AbsenceKind kind, TimeOnly? availableFrom, TimeOnly? availableUntil, string language)
    {
        var from = availableFrom.HasValue ? FormatTime(availableFrom.Value, language) : null;
        var until = availableUntil.HasValue ? FormatTime(availableUntil.Value, language) : null;

        return kind switch
        {
            AbsenceKind.LateArrival => FromPhrase(language, from!),
            AbsenceKind.EarlyLeave => UntilPhrase(language, until!),
            AbsenceKind.PartialWindow => $"{from} – {until}",
            _ => null,
        };
    }

    private static string FromPhrase(string language, string time) => language switch
    {
        "fr" => $"dès {time}",
        "de" => $"ab {time}",
        _ => $"from {time}",
    };

    private static string UntilPhrase(string language, string time) => language switch
    {
        "fr" => $"jusqu'à {time}",
        "de" => $"bis {time}",
        _ => $"until {time}",
    };

    /// <summary>French reads clock times as "21h30"; every other supported language keeps "21:30".</summary>
    private static string FormatTime(TimeOnly time, string language) =>
        language == "fr" ? time.ToString("HH'h'mm") : time.ToString("HH:mm");

    private static CultureInfo GetCulture(string language) => language switch
    {
        "fr" => CultureInfo.GetCultureInfo("fr-FR"),
        "de" => CultureInfo.GetCultureInfo("de-DE"),
        _ => CultureInfo.GetCultureInfo("en-US"),
    };
}
