namespace RaidOps.ExternalApplication.Contracts.Services.Discord;

/// <summary>
/// Notifies a Discord webhook that the application has finished starting up,
/// so each environment can announce its own deploys without relying on CI/Watchtower.
/// </summary>
public interface IDiscordDeployNotifier
{
    /// <summary>
    /// Posts a "now live" message to the webhook configured for the current environment
    /// (<c>Discord:DeployWebhookUrl</c>). Does nothing if no webhook URL is configured.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task NotifyDeployedAsync(CancellationToken cancellationToken = default);
}
