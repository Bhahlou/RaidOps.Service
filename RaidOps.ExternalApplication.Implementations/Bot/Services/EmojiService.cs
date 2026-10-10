using System.Collections.Concurrent;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.ExternalApplication.Implementations.Bot.Handlers;

namespace RaidOps.ExternalApplication.Implementations.Bot.Services;

/// <summary>
/// <inheritdoc cref="IEmojiService"/> Registered as a singleton (see
/// <c>ExternalApplicationsRegistry</c>) — <see cref="_emojiIdsByName"/> is synced once by
/// <see cref="ReadyHandler"/> at bot startup and then read from every later request-scoped
/// notification call, so it must outlive any single DI scope.
/// </summary>
public class EmojiService(
    GatewayClient gatewayClient,
    IHttpClientFactory httpClientFactory,
    ILogger<EmojiService> logger) : IEmojiService
{
    private readonly ConcurrentDictionary<string, ulong> _emojiIdsByName = new();

    /// <inheritdoc/>
    public async Task SyncAsync(IEnumerable<(string Name, string SourceUrl)> entries, CancellationToken cancellationToken = default)
    {
        var application = await gatewayClient.Rest.GetCurrentApplicationAsync(cancellationToken: cancellationToken);

        var existing = await gatewayClient.Rest.GetApplicationEmojisAsync(application.Id, cancellationToken: cancellationToken);
        foreach (var emoji in existing)
            _emojiIdsByName[emoji.Name] = emoji.Id;

        using var httpClient = httpClientFactory.CreateClient();

        foreach (var (name, sourceUrl) in entries)
        {
            if (_emojiIdsByName.ContainsKey(name))
                continue;

            try
            {
                var imageBytes = await httpClient.GetByteArrayAsync(sourceUrl, cancellationToken);
                var properties = new ApplicationEmojiProperties(name, new ImageProperties(ImageFormat.Jpeg, imageBytes));

                var created = await gatewayClient.Rest.CreateApplicationEmojiAsync(application.Id, properties, cancellationToken: cancellationToken);
                _emojiIdsByName[name] = created.Id;
                if (logger.IsEnabled(LogLevel.Information))
                    logger.LogInformation("Uploaded application emoji '{Name}'.", name);
            }
            catch (Exception ex)
            {
                // One bad manifest entry (dead URL, oversized image, ...) must never block the
                // rest — a missing emoji just means GetMarkdown falls back to plain text for it.
                logger.LogWarning(ex, "Failed to sync application emoji '{Name}' from {SourceUrl}.", name, sourceUrl);
            }
        }
    }

    /// <inheritdoc/>
    public string? GetMarkdown(string name) =>
        _emojiIdsByName.TryGetValue(name, out var id) ? $"<:{name}:{id}>" : null;

    /// <inheritdoc/>
    public ulong? GetId(string name) =>
        _emojiIdsByName.TryGetValue(name, out var id) ? id : null;
}
