using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RaidOps.API.Seeding;

/// <summary>
/// Idempotently imports the checked-in spell seed file(s) into the <see cref="Spell"/> reference
/// table on every startup — cheap once already seeded, since <see cref="ISpellRepository.InsertMissingAsync"/>
/// only inserts IDs it doesn't already have. Adding support for a new expansion is just committing
/// its own <c>spells.&lt;code&gt;.json</c> file and registering it in <see cref="SeedFiles"/> — no
/// runtime scraping, no manual per-deploy step.
/// </summary>
internal static class SpellSeeder
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    // (file name, expansion ID) — see Expansion's static seed data for the ID mapping.
    private static readonly (string FileName, int ExpansionId)[] SeedFiles = [("spells.tbc.json", 2)];

    public static async Task SeedAsync(ISpellRepository spellRepository, ILogger logger, CancellationToken cancellationToken)
    {
        foreach (var (fileName, expansionId) in SeedFiles)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "SeedData", fileName);
            if (!File.Exists(path))
            {
                logger.LogWarning("Spell seed file not found at {Path}; skipping.", path);
                continue;
            }

            var json = await File.ReadAllTextAsync(path, cancellationToken);
            var entries = JsonSerializer.Deserialize<List<SpellSeedEntry>>(json, JsonOptions) ?? [];

            var spells = entries.Select(e => new Spell
            {
                Id = e.Id,
                ExpansionId = expansionId,
                NameEn = e.NameEn,
                NameFr = e.NameFr,
                NameDe = e.NameDe,
                IconUrl = e.IconUrl,
            });

            var inserted = await spellRepository.InsertMissingAsync(spells, cancellationToken);
            if (inserted > 0)
                logger.LogInformation("Seeded {Count} new spells from {FileName}.", inserted, fileName);
        }
    }

    private class SpellSeedEntry
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("nameEn")] public string NameEn { get; set; } = string.Empty;
        [JsonPropertyName("nameFr")] public string NameFr { get; set; } = string.Empty;
        [JsonPropertyName("nameDe")] public string NameDe { get; set; } = string.Empty;
        [JsonPropertyName("iconUrl")] public string IconUrl { get; set; } = string.Empty;
    }
}
