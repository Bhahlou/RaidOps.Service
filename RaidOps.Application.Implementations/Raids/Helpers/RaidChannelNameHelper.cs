using System.Text.RegularExpressions;

namespace RaidOps.Application.Implementations.Raids.Helpers;

/// <summary>
/// Builds a suggested Discord channel name for a single raid occurrence — `&lt;raid name&gt;
/// &lt;short weekday&gt; &lt;day&gt; &lt;short month&gt;`, localized to the guild's language. Mirrors
/// the front end's own `buildRaidChannelName` (used by the create/edit raid dialogs), needed here
/// too since series occurrences get their per-occurrence channel auto-created server-side at
/// materialization time, with nobody in the loop to type a name. No <c>CultureInfo</c> involved —
/// this image runs globalization-invariant (see <c>feedback_no_noninvariant_cultureinfo_backend</c>),
/// so weekday/month abbreviations are hardcoded per language like the rest of this project's
/// per-language text (see <see cref="RaidNotificationText"/>).
/// </summary>
public static partial class RaidChannelNameHelper
{
    private static readonly string[] ShortWeekdaysEn = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];
    private static readonly string[] ShortWeekdaysFr = ["dim", "lun", "mar", "mer", "jeu", "ven", "sam"];
    private static readonly string[] ShortWeekdaysDe = ["So", "Mo", "Di", "Mi", "Do", "Fr", "Sa"];

    private static readonly string[] ShortMonthsEn = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
    private static readonly string[] ShortMonthsFr = ["janv", "févr", "mars", "avr", "mai", "juin", "juil", "août", "sept", "oct", "nov", "déc"];
    private static readonly string[] ShortMonthsDe = ["Jan", "Feb", "Mär", "Apr", "Mai", "Jun", "Jul", "Aug", "Sep", "Okt", "Nov", "Dez"];

    /// <param name="raidName">The raid's own name (e.g. the series name for a materialized occurrence).</param>
    /// <param name="localDate">The occurrence's date, already resolved to the guild's local timezone.</param>
    /// <param name="language">Guild language code (<c>"fr"</c>/<c>"de"</c>/anything else falls back to English).</param>
    public static string BuildChannelName(string raidName, DateOnly localDate, string? language)
    {
        var (weekdays, months) = language switch
        {
            "fr" => (ShortWeekdaysFr, ShortMonthsFr),
            "de" => (ShortWeekdaysDe, ShortMonthsDe),
            _ => (ShortWeekdaysEn, ShortMonthsEn),
        };

        var datePart = $"{weekdays[(int)localDate.DayOfWeek]} {localDate.Day} {months[localDate.Month - 1]}";
        return Slugify($"{raidName} {datePart}");
    }

    /// <summary>Lowercase, spaces to hyphens, strips punctuation — keeps Unicode letters (accents included). Mirrors the front end's `slugifyChannelName`.</summary>
    private static string Slugify(string raw)
    {
        var stripped = StripInvalidCharsRegex().Replace(raw.ToLowerInvariant(), "").Trim();
        var hyphenated = WhitespaceRegex().Replace(stripped, "-");
        var collapsed = RepeatedHyphensRegex().Replace(hyphenated, "-");
        return collapsed.Length > 100 ? collapsed[..100] : collapsed;
    }

    [GeneratedRegex(@"[^\p{L}\p{N}\s-]")]
    private static partial Regex StripInvalidCharsRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"-+")]
    private static partial Regex RepeatedHyphensRegex();
}
