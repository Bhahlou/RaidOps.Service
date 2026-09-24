using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Spells.Commands;
using RaidOps.Application.Contracts.Raids.Spells.Responses;
using RaidOps.Domain.Models.Reference;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.ExternalApplication.Contracts.Services.WagoTools;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;
using System.Collections.Concurrent;

namespace RaidOps.Application.Implementations.Raids.Spells.CommandHandlers;

/// <summary>
/// Handles <see cref="SyncSpellsCommand"/> — polls wago.tools for the latest build of every active,
/// wago-tracked branch and, for any branch whose build changed (or unconditionally when
/// <see cref="SyncSpellsCommand.Force"/>), pulls the current <c>SpellName</c>/<c>SpellMisc</c> DB2
/// exports and upserts the <see cref="Spell"/> reference table.
/// </summary>
public class SyncSpellsCommandHandler(
    IBranchRepository branchRepository,
    IWagoToolsService wagoToolsService,
    ISpellRepository spellRepository,
    IDiscordBotService discordBotService,
    IConfiguration configuration,
    ILogger<SyncSpellsCommandHandler> logger)
    : ICommandHandlerAsync<SyncSpellsCommand>
{
    private const int MaxSampleEntriesPerField = 10;
    private const int IconResolutionConcurrency = 16;

    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(SyncSpellsCommand command, CancellationToken cancellationToken = default)
    {
        var branches = await branchRepository.GetActiveWagoTrackedAsync(cancellationToken);
        if (branches.Count == 0)
            return Result<CommandResponse>.Ok(new CommandResponse("No wago.tools-tracked branch is active.", new List<BranchSyncResult>()));

        var latestBuilds = await wagoToolsService.GetLatestBuildsAsync(cancellationToken);

        var results = new List<BranchSyncResult>();
        var changesByBranch = new List<BranchChange>();

        foreach (var branch in branches)
        {
            if (!latestBuilds.TryGetValue(branch.WagoProductCode!, out var buildInfo))
            {
                logger.LogWarning(
                    "wago.tools has no build info for product {ProductCode} (branch {BranchName}); skipping.",
                    branch.WagoProductCode, branch.Name);
                continue;
            }

            var skipped = !command.Force && buildInfo.Version == branch.LastSyncedBuildVersion;
            var result = new BranchSyncResult
            {
                BranchId = branch.Id,
                BranchName = branch.Name,
                PreviousBuild = branch.LastSyncedBuildVersion,
                LatestBuild = buildInfo.Version,
                Skipped = skipped,
            };
            results.Add(result);

            if (skipped)
                continue;

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("Spell sync: syncing {BranchName} to build {Build}...", branch.Name, buildInfo.Version);

            var diff = await SyncBranchAsync(branch, buildInfo.Version, cancellationToken);
            result.AddedCount = diff.Added.Count;
            result.RenamedCount = diff.Renamed.Count;

            await branchRepository.UpdateSyncStateAsync(branch.Id, buildInfo.Version, buildInfo.CreatedAt, cancellationToken);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "Spell sync: {BranchName} synced to build {Build} — {Added} added, {Renamed} renamed.",
                    branch.Name, buildInfo.Version, diff.Added.Count, diff.Renamed.Count);
            }

            if (diff.Added.Count > 0 || diff.Renamed.Count > 0)
                changesByBranch.Add(new BranchChange(branch.Name, buildInfo.Version, branch.LastSyncedBuildVersion, diff));
        }

        if (changesByBranch.Count > 0)
        {
            // The sync itself (everything above) already succeeded and is saved — a bad Discord
            // channel config/permission shouldn't turn that into a reported failure.
            try
            {
                await NotifyDiscordAsync(changesByBranch, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Deliberately broad: IDiscordBotService/IMessageService are SDK-agnostic on
                // purpose (see DiscordEmbedContent's doc comment) specifically so this layer
                // never needs a NetCord package reference just to catch its exception type.
                logger.LogWarning(ex, "Spell sync succeeded but the Discord notification failed to send.");
            }
        }

        return Result<CommandResponse>.Ok(new CommandResponse($"{results.Count} branch(es) checked.", results));
    }

    private async Task<SpellSyncDiff> SyncBranchAsync(Branch branch, string build, CancellationToken cancellationToken)
    {
        var enNames = await wagoToolsService.GetSpellNamesAsync(build, "enUS", cancellationToken);
        var frNames = await wagoToolsService.GetSpellNamesAsync(build, "frFR", cancellationToken);
        var deNames = await wagoToolsService.GetSpellNamesAsync(build, "deDE", cancellationToken);
        var iconFileDataIds = await wagoToolsService.GetSpellIconFileDataIdsAsync(build, cancellationToken);

        var frById = frNames.ToDictionary(s => s.Id, s => s.Name);
        var deById = deNames.ToDictionary(s => s.Id, s => s.Name);

        // Most spells share an icon (per-rank/per-difficulty variants) — resolve each distinct
        // FileDataID at most once, and in parallel (bounded), rather than once-per-spell,
        // sequentially. Thousands of distinct icons at once would otherwise take hours.
        var iconUrlByFileDataId = await ResolveIconUrlsAsync(iconFileDataIds.Values.Distinct().ToList(), build, cancellationToken);

        var rows = enNames.Select(en => new SpellAvailability
        {
            SpellId = en.Id,
            ExpansionId = branch.CurrentExpansionId,
            NameEn = en.Name,
            NameFr = frById.GetValueOrDefault(en.Id, en.Name),
            NameDe = deById.GetValueOrDefault(en.Id, en.Name),
            IconUrl = iconFileDataIds.TryGetValue(en.Id, out var fileDataId) && iconUrlByFileDataId.TryGetValue(fileDataId, out var iconUrl)
                ? iconUrl
                : string.Empty,
        }).ToList();

        return await spellRepository.UpsertAsync(rows, cancellationToken);
    }

    private async Task<Dictionary<int, string>> ResolveIconUrlsAsync(List<int> fileDataIds, string build, CancellationToken cancellationToken)
    {
        var iconUrlByFileDataId = new ConcurrentDictionary<int, string>();
        var iconBaseUrl = configuration["Blizzard:SpellIconBaseUrl"]
            ?? throw new InvalidOperationException("Blizzard:SpellIconBaseUrl is not configured.");

        var options = new ParallelOptions { MaxDegreeOfParallelism = IconResolutionConcurrency, CancellationToken = cancellationToken };
        await Parallel.ForEachAsync(fileDataIds, options, async (fileDataId, ct) =>
        {
            // A stale/dangling FileDataID (removed from the client since this build's SpellMisc
            // export was generated) shouldn't abort the whole branch's sync — fall back to no icon
            // for just that one and keep going.
            try
            {
                var fileName = await wagoToolsService.GetFileNameAsync(fileDataId, build, ct);
                iconUrlByFileDataId[fileDataId] = BuildIconUrl(iconBaseUrl, fileName);
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "Failed to resolve icon FileDataID {FileDataId} for build {Build}; affected spells are left without an icon.", fileDataId, build);
                iconUrlByFileDataId[fileDataId] = string.Empty;
            }
        });

        return new Dictionary<int, string>(iconUrlByFileDataId);
    }

    private static string BuildIconUrl(string iconBaseUrl, string fileName)
    {
        var iconName = Path.GetFileNameWithoutExtension(fileName.Replace('\\', '/'));
        return $"{iconBaseUrl}{iconName}.jpg";
    }

    private async Task NotifyDiscordAsync(List<BranchChange> changesByBranch, CancellationToken cancellationToken)
    {
        var channelIdSetting = configuration["Discord:SpellSyncChannelId"];
        if (string.IsNullOrWhiteSpace(channelIdSetting))
        {
            logger.LogWarning("Spell sync found changes but Discord:SpellSyncChannelId isn't configured; skipping notification.");
            return;
        }

        var fields = changesByBranch.Select(c => new DiscordEmbedField(BuildFieldName(c), BuildFieldValue(c.Diff))).ToList();
        var embed = new DiscordEmbedContent(
            Title: "Spell data synced",
            Description: $"{changesByBranch.Count} branch(es) had spell changes this run.",
            Fields: fields);

        await discordBotService.Messages.SendEmbedAsync(ulong.Parse(channelIdSetting), embed, cancellationToken);
    }

    // "Retail · 12.1.0.69933", or "Retail · 12.1.0.69800 → 12.1.0.69933" when moving up from a previously synced build.
    private static string BuildFieldName(BranchChange change) =>
        change.PreviousBuild is null || change.PreviousBuild == change.Build
            ? $"{change.BranchName} · {change.Build}"
            : $"{change.BranchName} · {change.PreviousBuild} → {change.Build}";

    private sealed record BranchChange(string BranchName, string Build, string? PreviousBuild, SpellSyncDiff Diff);

    private static string BuildFieldValue(SpellSyncDiff diff)
    {
        var lines = new List<string> { $"**{diff.Added.Count}** added, **{diff.Renamed.Count}** renamed" };

        if (diff.Added.Count > 0)
            lines.Add(FormatSample("Added", diff.Added.Select(e => e.NameEn)));

        if (diff.Renamed.Count > 0)
            lines.Add(FormatSample("Renamed", diff.Renamed.Select(e => $"{e.PreviousNameEn} → {e.NameEn}")));

        return string.Join('\n', lines);
    }

    private static string FormatSample(string label, IEnumerable<string> entries)
    {
        var list = entries.ToList();
        var sample = list.Take(MaxSampleEntriesPerField);
        var more = list.Count > MaxSampleEntriesPerField ? $" (+{list.Count - MaxSampleEntriesPerField} more)" : string.Empty;
        return $"{label}: {string.Join(", ", sample)}{more}";
    }
}
