using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Spells.Commands;

namespace RaidOps.API.BackgroundServices;

/// <summary>
/// Polls wago.tools hourly for new builds of every active, wago-tracked branch and keeps the
/// <see cref="RaidOps.Domain.Models.Reference.Spell"/> reference table in sync — see
/// <see cref="SyncSpellsCommand"/>/<c>SyncSpellsCommandHandler</c> for the actual sync logic. Runs
/// once immediately on startup, then every <see cref="PollInterval"/>. A hosted service is a
/// singleton, but <see cref="ICommandDispatcher"/> and everything it resolves are scoped, so each
/// tick creates its own scope — same pattern as the Discord bot's gateway handlers
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
        using var timer = new PeriodicTimer(PollInterval);

        do
        {
            try
            {
                await SyncAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Spell sync poll failed.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
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
