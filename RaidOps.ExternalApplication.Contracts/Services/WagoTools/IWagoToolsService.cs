using RaidOps.ExternalApplication.Contracts.Services.WagoTools.Responses;

namespace RaidOps.ExternalApplication.Contracts.Services.WagoTools;

/// <summary>
/// Abstraction over the public, unauthenticated wago.tools API — used to keep the <c>Spell</c>
/// reference table in sync with live WoW branches (e.g. Forever) that ship new builds frequently,
/// instead of the one-time checked-in JSON dump used for frozen expansions.
/// </summary>
public interface IWagoToolsService
{
    /// <summary>
    /// Fetches the current build for every product wago.tools tracks, from
    /// <c>GET https://wago.tools/api/builds/latest</c>. Keyed by product code (e.g. <c>"wow_classic_beta"</c>).
    /// </summary>
    Task<Dictionary<string, WagoBuildInfo>> GetLatestBuildsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches every spell ID + localized name for <paramref name="build"/> from the <c>SpellName</c>
    /// DB2 table, via <c>GET /db2/SpellName/csv?build={build}&amp;locale={locale}</c>.
    /// </summary>
    /// <param name="build">The full build version, e.g. <c>"1.60.1.69977"</c>.</param>
    /// <param name="locale">A wago.tools client locale code, e.g. <c>"enUS"</c>, <c>"frFR"</c>, <c>"deDE"</c>.</param>
    Task<List<WagoSpellName>> GetSpellNamesAsync(string build, string locale, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches each spell's icon <c>FileDataID</c> for <paramref name="build"/> from the
    /// <c>SpellMisc</c> DB2 table (locale-independent). Spells with no icon set are omitted.
    /// </summary>
    /// <returns>Spell ID → icon FileDataID.</returns>
    Task<Dictionary<int, int>> GetSpellIconFileDataIdsAsync(string build, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a CASC <c>FileDataID</c> to its real Blizzard client filename (e.g.
    /// <c>"interface/icons/inv_misc_food_59.blp"</c>) via
    /// <c>GET /api/info/{fileDataId}?version={build}</c>. The <paramref name="build"/> is required —
    /// wago.tools returns 400 without it, since it otherwise has no build to resolve the FDID against.
    /// </summary>
    Task<string> GetFileNameAsync(int fileDataId, string build, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads the community WoW listfile (the URL comes from <c>Wago:ListfileUrl</c>, streamed — it
    /// is ~150 MB) and returns only the icon entries: FileDataID → bare icon name (e.g.
    /// <c>"inv_misc_food_59"</c>). One request replaces tens of thousands of per-icon
    /// <see cref="GetFileNameAsync"/> lookups; icons too new to be in it are looked up individually.
    /// </summary>
    Task<Dictionary<int, string>> GetIconFileNamesAsync(CancellationToken cancellationToken = default);
}
