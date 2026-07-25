namespace RaidOps.Application.Implementations.Guilds.Registration.Helpers;

/// <summary>
/// Maps a Discord locale code (e.g. <c>"fr"</c>, <c>"de"</c>, <c>"en-US"</c>, <c>"pt-BR"</c>) to
/// one of RaidOps' supported languages ("en", "fr", "de"). Falls back to "en" for anything else —
/// including <c>null</c>, which is what an un-configured (non-Community) Discord guild reports.
/// </summary>
internal static class DiscordLocaleMapper
{
    private static readonly HashSet<string> SupportedLanguages = ["en", "fr", "de"];

    public static string ToAppLanguage(string? discordLocale)
    {
        if (string.IsNullOrEmpty(discordLocale))
            return "en";

        var language = discordLocale.Split('-')[0].ToLowerInvariant();
        return SupportedLanguages.Contains(language) ? language : "en";
    }
}
