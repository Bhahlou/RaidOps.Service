using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Spells.Commands;

namespace RaidOps.API.BackgroundServices;

/// <summary>
/// Polls wago.tools hourly for new builds of every active, wago-tracked branch and keeps the
/// <see cref="RaidOps.Domain.Models.Reference.SpellAvailability"/> reference data in sync — see
/// <see cref="SyncSpellsCommand"/>/<c>SyncSpellsCommandHandler</c> for the actual sync logic. Runs
/// once immediately on startup, then waits <see cref="PollInterval"/> after each run. A hosted service
/// is a singleton, but <see cref="ICommandDispatcher"/> and everything it resolves are scoped, so each
/// run creates its own scope — same pattern as the Discord bot's gateway handlers
/// (<c>GuildDeleteHandler</c> et al.).
/// </summary>
public class SpellSyncBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<SpellSyncBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(1);

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Spell sync poll failed.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Shutdown requested while waiting — the loop condition ends the service cleanly.
            }
        }
    }

    private async Task SyncAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();

        var result = await dispatcher.DispatchAsync(new SyncSpellsCommand { Force = false }, cancellationToken);
        if (result.IsFailed && logger.IsEnabled(LogLevel.Warning))
            logger.LogWarning("Spell sync poll returned a failure: {Error}", result.Error);
    }
}
