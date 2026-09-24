using RaidOps.ExternalApplication.Contracts.Services.WagoTools;
using RaidOps.ExternalApplication.Contracts.Services.WagoTools.Responses;
using System.Text;
using System.Text.Json;

namespace RaidOps.ExternalApplication.Implementations.Services;

/// <summary>
/// HTTP client implementation of <see cref="IWagoToolsService"/> that calls the public,
/// unauthenticated wago.tools API and DB2 CSV exports.
/// </summary>
public class WagoToolsService(HttpClient httpClient) : IWagoToolsService
{
    private const string BaseUrl = "https://wago.tools";

    /// <inheritdoc/>
    public async Task<Dictionary<string, WagoBuildInfo>> GetLatestBuildsAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"{BaseUrl}/api/builds/latest", cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<Dictionary<string, WagoBuildInfo>>(content)
            ?? throw new InvalidOperationException("Failed to deserialize wago.tools builds/latest response.");
    }

    /// <inheritdoc/>
    public async Task<List<WagoSpellName>> GetSpellNamesAsync(string build, string locale, CancellationToken cancellationToken = default)
    {
        var rows = await GetCsvRowsAsync($"{BaseUrl}/db2/SpellName/csv?build={build}&locale={locale}", cancellationToken);

        return rows.Skip(1)
            .Where(row => row.Length >= 2)
            .Select(row => new WagoSpellName { Id = int.Parse(row[0]), Name = row[1] })
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<Dictionary<int, int>> GetSpellIconFileDataIdsAsync(string build, CancellationToken cancellationToken = default)
    {
        var rows = await GetCsvRowsAsync($"{BaseUrl}/db2/SpellMisc/csv?build={build}", cancellationToken);
        var header = rows[0];
        var spellIdIndex = Array.IndexOf(header, "SpellID");
        var iconIndex = Array.IndexOf(header, "SpellIconFileDataID");
        if (spellIdIndex < 0 || iconIndex < 0)
            throw new InvalidOperationException("wago.tools SpellMisc export is missing the SpellID/SpellIconFileDataID columns.");

        var result = new Dictionary<int, int>();
        foreach (var row in rows.Skip(1))
        {
            var spellId = int.Parse(row[spellIdIndex]);
            var fileDataId = int.Parse(row[iconIndex]);
            if (fileDataId > 0)
                result[spellId] = fileDataId;
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<string> GetFileNameAsync(int fileDataId, string build, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"{BaseUrl}/api/info/{fileDataId}?version={build}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var info = JsonSerializer.Deserialize<WagoFileInfoResponse>(content)
            ?? throw new InvalidOperationException($"Failed to deserialize wago.tools file info for FileDataID {fileDataId}.");
        return info.Filename;
    }

    private async Task<string[][]> GetCsvRowsAsync(string url, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return content
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.Length > 0)
            .Select(ParseCsvLine)
            .ToArray();
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

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (inQuotes)
            {
                if (c != '"')
                {
                    current.Append(c);
                    continue;
                }

                if (i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
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
