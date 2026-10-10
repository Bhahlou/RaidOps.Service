using RaidOps.ExternalApplication.Contracts.Services.WagoTools;
using RaidOps.ExternalApplication.Contracts.Services.WagoTools.Responses;

namespace RaidOps.IntegrationTests.Infrastructure.Stubs;

/// <summary>
/// Configurable in-memory <see cref="IWagoToolsService"/> used in integration tests so that neither the
/// hourly <c>SpellSyncBackgroundService</c> (which fires as soon as the test host starts) nor the admin
/// sync endpoint ever reaches the real wago.tools. By default it reports no builds at all, which makes
/// every sync a no-op; a test that wants the sync to do something fills in <see cref="LatestBuilds"/> and
/// the per-build content, and must call <see cref="Reset"/> when it is done.
/// </summary>
internal class StubWagoToolsService : IWagoToolsService
{
    private readonly object _lock = new();

    /// <summary>Product code → latest build, as <c>/api/builds/latest</c> would return it.</summary>
    public Dictionary<string, WagoBuildInfo> LatestBuilds { get; set; } = [];

    /// <summary>Wago locale code (<c>enUS</c>/<c>frFR</c>/<c>deDE</c>) → the spell names returned for any build.</summary>
    public Dictionary<string, List<WagoSpellName>> SpellNamesByLocale { get; set; } = [];

    /// <summary>Spell ID → icon FileDataID, as the <c>SpellMisc</c> export would return it.</summary>
    public Dictionary<int, int> IconFileDataIds { get; set; } = [];

    /// <summary>FileDataID → client file name, as <c>/api/info/{id}</c> would return it.</summary>
    public Dictionary<int, string> FileNames { get; set; } = [];

    /// <summary>FileDataID → icon name (no path/extension), as the community listfile would yield it. Empty by default.</summary>
    public Dictionary<int, string> IconFileNames { get; set; } = [];

    /// <inheritdoc/>
    public Task<Dictionary<string, WagoBuildInfo>> GetLatestBuildsAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
            return Task.FromResult(new Dictionary<string, WagoBuildInfo>(LatestBuilds));
    }

    /// <inheritdoc/>
    public Task<List<WagoSpellName>> GetSpellNamesAsync(string build, string locale, CancellationToken cancellationToken = default)
    {
        lock (_lock)
            return Task.FromResult(SpellNamesByLocale.TryGetValue(locale, out var names) ? names.ToList() : []);
    }

    /// <inheritdoc/>
    public Task<Dictionary<int, int>> GetSpellIconFileDataIdsAsync(string build, CancellationToken cancellationToken = default)
    {
        lock (_lock)
            return Task.FromResult(new Dictionary<int, int>(IconFileDataIds));
    }

    /// <inheritdoc/>
    public Task<string> GetFileNameAsync(int fileDataId, string build, CancellationToken cancellationToken = default)
    {
        lock (_lock)
            return Task.FromResult(FileNames.TryGetValue(fileDataId, out var fileName)
                ? fileName
                : throw new HttpRequestException($"No file for FileDataID {fileDataId}."));
    }

    /// <inheritdoc/>
    public Task<Dictionary<int, string>> GetIconFileNamesAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
            return Task.FromResult(new Dictionary<int, string>(IconFileNames));
    }

    /// <summary>Makes every sync a no-op again.</summary>
    public void Reset()
    {
        lock (_lock)
        {
            LatestBuilds = [];
            SpellNamesByLocale = [];
            IconFileDataIds = [];
            FileNames = [];
            IconFileNames = [];
        }
    }
}
