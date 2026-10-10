using Microsoft.Extensions.Configuration;
using RaidOps.ExternalApplication.Contracts.Services.WagoTools;
using RaidOps.ExternalApplication.Contracts.Services.WagoTools.Responses;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace RaidOps.ExternalApplication.Implementations.Services;

/// <summary>
/// HTTP client implementation of <see cref="IWagoToolsService"/> that calls the public,
/// unauthenticated wago.tools API and DB2 CSV exports. The base address comes from the
/// <c>Wago:BaseUrl</c> setting, applied when the typed client is registered. The large CSV exports
/// (hundreds of thousands of rows for Retail) are streamed line by line rather than buffered whole, so
/// a sync stays light enough for a small container.
/// </summary>
public class WagoToolsService(HttpClient httpClient, IConfiguration configuration) : IWagoToolsService
{
    private const string IconPathPrefix = "interface/icons/";

    /// <inheritdoc/>
    public async Task<Dictionary<string, WagoBuildInfo>> GetLatestBuildsAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync("api/builds/latest", cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<Dictionary<string, WagoBuildInfo>>(content)
            ?? throw new InvalidOperationException("Failed to deserialize wago.tools builds/latest response.");
    }

    /// <inheritdoc/>
    public async Task<List<WagoSpellName>> GetSpellNamesAsync(string build, string locale, CancellationToken cancellationToken = default)
    {
        var names = new List<WagoSpellName>();
        var isHeader = true;

        await foreach (var row in ReadCsvRowsAsync($"db2/SpellName/csv?build={build}&locale={locale}", cancellationToken))
        {
            if (isHeader)
            {
                isHeader = false;
                continue;
            }

            if (row.Length >= 2)
                names.Add(new WagoSpellName { Id = int.Parse(row[0]), Name = row[1] });
        }

        return names;
    }

    /// <inheritdoc/>
    public async Task<Dictionary<int, int>> GetSpellIconFileDataIdsAsync(string build, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<int, int>();
        var spellIdIndex = -1;
        var iconIndex = -1;
        var isHeader = true;

        await foreach (var row in ReadCsvRowsAsync($"db2/SpellMisc/csv?build={build}", cancellationToken))
        {
            if (isHeader)
            {
                isHeader = false;
                spellIdIndex = Array.IndexOf(row, "SpellID");
                iconIndex = Array.IndexOf(row, "SpellIconFileDataID");
                if (spellIdIndex < 0 || iconIndex < 0)
                    throw new InvalidOperationException("wago.tools SpellMisc export is missing the SpellID/SpellIconFileDataID columns.");
                continue;
            }

            var fileDataId = int.Parse(row[iconIndex]);
            if (fileDataId > 0)
                result[int.Parse(row[spellIdIndex])] = fileDataId;
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<string> GetFileNameAsync(int fileDataId, string build, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"api/info/{fileDataId}?version={build}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var info = JsonSerializer.Deserialize<WagoFileInfoResponse>(content)
            ?? throw new InvalidOperationException($"Failed to deserialize wago.tools file info for FileDataID {fileDataId}.");
        return info.Filename;
    }

    /// <inheritdoc/>
    public async Task<Dictionary<int, string>> GetIconFileNamesAsync(CancellationToken cancellationToken = default)
    {
        var listfileUrl = configuration["Wago:ListfileUrl"]
            ?? throw new InvalidOperationException("Wago:ListfileUrl is not configured.");

        var icons = new Dictionary<int, string>();

        using var response = await httpClient.GetAsync(listfileUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        // Lines look like "134015;interface/icons/inv_misc_food_59.blp" — only icons are kept, out of
        // ~2M entries, so the map stays small (~40k) even though the file is large.
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            var separator = line.IndexOf(';');
            if (separator <= 0)
                continue;

            var path = line.AsSpan(separator + 1);
            if (!path.StartsWith(IconPathPrefix, StringComparison.OrdinalIgnoreCase) || !int.TryParse(line.AsSpan(0, separator), out var fileDataId))
                continue;

            icons[fileDataId] = Path.GetFileNameWithoutExtension(path.ToString());
        }

        return icons;
    }

    private async IAsyncEnumerable<string[]> ReadCsvRowsAsync(string url, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.Length > 0)
                yield return ParseCsvLine(line);
        }
    }

    /// <summary>
    /// Splits one RFC4180 CSV line into fields, honoring quoted fields (which may contain commas)
    /// and doubled-quote escaping (<c>""</c> inside a quoted field is a literal <c>"</c>).
    /// </summary>
    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        var index = 0;
        while (index < line.Length)
        {
            var c = line[index++];

            if (inQuotes)
            {
                if (c != '"')
                {
                    current.Append(c);
                    continue;
                }

                if (index < line.Length && line[index] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = false;
                }
                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    fields.Add(current.ToString());
                    current.Clear();
                    break;
                default:
                    current.Append(c);
                    break;
            }
        }

        fields.Add(current.ToString());
        return fields.ToArray();
    }
}
