using RaidOps.Application.Contracts.Specs.Responses;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.ExternalApplication.Implementations.Bot.Handlers;

namespace RaidOps.ExternalApplication.Implementations.Bot.Services;

/// <summary>
/// Every application emoji <see cref="EmojiService"/> should keep synced, as (name, source image
/// URL) pairs. Add a new *static* category here (a new hardcoded icon set) and it gets uploaded on
/// the next bot startup on every environment automatically. DB-seeded categories (specs today,
/// anything similar later) instead need their data fetched by the caller (see
/// <see cref="ReadyHandler"/>, which dispatches <c>GetSpecsQuery</c>) and passed to
/// <see cref="SpecIcons"/> — this class has no DB access of its own by design (see
/// RaidOps.ExternalApplication.Implementations.csproj's project references).
/// </summary>
internal static class ApplicationEmojiManifest
{
    /// <summary>
    /// WoW class icons, sourced directly from the same Blizzard CDN the front end uses
    /// (<c>wow-class-icon.component.ts</c>). <paramref name="blizzardClassIconBaseUrl"/> comes from
    /// the <c>Discord:BlizzardClassIconBaseUrl</c> config key (see <see cref="ReadyHandler"/>) —
    /// identical on every environment, so it's set once in the base <c>appsettings.json</c> rather
    /// than hardcoded here, with no per-environment override needed.
    /// </summary>
    public static IEnumerable<(string Name, string SourceUrl)> ClassIcons(string blizzardClassIconBaseUrl) => WowClassEmojiNames.ByClassId.Values
        .Select(name => (name, $"{blizzardClassIconBaseUrl}{name["class_".Length..]}.jpg"));

    /// <summary>WoW spec icons, from the seeded <c>Spec.IconUrl</c> field — skips any spec that hasn't got one synced yet.</summary>
    public static IEnumerable<(string Name, string SourceUrl)> SpecIcons(IEnumerable<SpecDto> specs) => specs
        .Where(s => s.IconUrl != null)
        .Select(s => (WowSpecEmojiNames.GetName(s.ClassId, s.Name), s.IconUrl!));
}
