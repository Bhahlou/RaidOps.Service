using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RaidOps.ExternalApplication.Contracts.Services.Discord;
using System.Net.Http.Json;

namespace RaidOps.ExternalApplication.Implementations.Services;

/// <summary>
/// HTTP client implementation of <see cref="IDiscordDeployNotifier"/> that posts a
/// "now live" embed to the Discord webhook configured for the current environment.
/// </summary>
public class DiscordDeployNotifier(
    HttpClient httpClient,
    IConfiguration configuration,
    IHostEnvironment hostEnvironment,
    ILogger<DiscordDeployNotifier> logger) : IDiscordDeployNotifier
{
    /// <summary>
    /// Posts the "now live" message to <c>Discord:DeployWebhookUrl</c>. Swallows and logs
    /// any failure instead of throwing, so a Discord outage can never block app startup.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    public async Task NotifyDeployedAsync(CancellationToken cancellationToken = default)
    {
        var webhookUrl = configuration["Discord:DeployWebhookUrl"];
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("No Discord:DeployWebhookUrl configured, skipping deploy notification.");
            return;
        }

        var version = configuration["APP_VERSION"] ?? "dev";
        var payload = new
        {
            embeds = new[]
            {
                new
                {
                    title = $"✅ RaidOps {hostEnvironment.EnvironmentName} {version} is live",
                    color = 5763719
                }
            }
        };

        try
        {
            var response = await httpClient.PostAsJsonAsync(webhookUrl, payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
                logger.LogWarning("Discord deploy notification failed with status {StatusCode}.", response.StatusCode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while sending the Discord deploy notification.");
        }
    }
}
