namespace RaidOps.Application.Implementations.Raids.Helpers;

/// <summary>
/// Resolves a guild's local wall-clock time from a UTC instant (and back) — used to evaluate
/// lockout windows and absence declarations (both stored/keyed as <see cref="DateOnly"/> local
/// dates, with <see cref="DateOnly"/>/<see cref="TimeOnly"/>-bound partial-availability windows)
/// from a <c>RaidEvent.StartsAtUtc</c> instant.
/// </summary>
public static class GuildTimeHelper
{
    /// <summary>
    /// Converts <paramref name="utc"/> to the local wall-clock instant in <paramref name="ianaTimezone"/>.
    /// Falls back to treating <paramref name="utc"/> as already-local when the guild hasn't
    /// configured a timezone yet, or when the configured identifier can't be resolved.
    /// </summary>
    public static DateTime ToGuildLocalDateTime(DateTime utc, string? ianaTimezone)
    {
        if (string.IsNullOrWhiteSpace(ianaTimezone))
            return utc;

        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(ianaTimezone);
            var utcInstant = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(utcInstant, timeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            return utc;
        }
        catch (InvalidTimeZoneException)
        {
            return utc;
        }
    }

    /// <summary>Converts <paramref name="utc"/> to the local calendar date in <paramref name="ianaTimezone"/>. See <see cref="ToGuildLocalDateTime"/> for the fallback behavior.</summary>
    public static DateOnly ToGuildLocalDate(DateTime utc, string? ianaTimezone)
        => DateOnly.FromDateTime(ToGuildLocalDateTime(utc, ianaTimezone));

    /// <summary>
    /// Converts a wall-clock <paramref name="local"/> instant in <paramref name="ianaTimezone"/> to
    /// UTC — the inverse of <see cref="ToGuildLocalDateTime"/>, used when materializing a
    /// <c>RaidSeries</c>'s local recurrence day/time into a concrete <c>RaidEvent.StartsAtUtc</c>.
    /// Falls back to treating <paramref name="local"/> as already-UTC when the guild hasn't
    /// configured a timezone yet, or when the configured identifier can't be resolved.
    /// </summary>
    public static DateTime FromGuildLocal(DateTime local, string? ianaTimezone)
    {
        if (string.IsNullOrWhiteSpace(ianaTimezone))
            return DateTime.SpecifyKind(local, DateTimeKind.Utc);

        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(ianaTimezone);
            var unspecifiedLocal = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(unspecifiedLocal, timeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            return DateTime.SpecifyKind(local, DateTimeKind.Utc);
        }
        catch (InvalidTimeZoneException)
        {
            return DateTime.SpecifyKind(local, DateTimeKind.Utc);
        }
    }
}
