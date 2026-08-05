using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RaidOps.Application.Contracts.Services;

namespace RaidOps.ExternalApplication.Implementations.Bot;

/// <summary>
/// Shared push-and-refetch logic for the <c>GUILD_MEMBER_UPDATE</c>/<c>ADD</c>/<c>REMOVE</c>
/// gateway handlers: no diffing, just tells the affected user's connected clients (if any) to
/// re-sync via <see cref="IAuthNotifier"/>. Resolves <see cref="IAuthNotifier"/> from a fresh
/// <see cref="IServiceScope"/> per event — NetCord registers gateway handlers as singletons.
/// Never lets a notification failure take the gateway connection down; worst case, the user
/// only gets the data update on their next reactive token refresh.
/// </summary>
internal static class GuildUserChangeNotifier
{
    internal static async ValueTask NotifyAsync(
        IServiceScopeFactory scopeFactory,
        ILogger logger,
        string eventName,
        ulong discordId,
        ulong guildId)
    {
        try
        {
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("{EventName} received for user {DiscordId} in guild {GuildId}.", eventName, discordId, guildId);

            await using var scope = scopeFactory.CreateAsyncScope();
            var authNotifier = scope.ServiceProvider.GetRequiredService<IAuthNotifier>();

            await authNotifier.NotifyDiscordDataChangedAsync(discordId.ToString());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to notify user {DiscordId} of a Discord data change.", discordId);
        }
    }
}
