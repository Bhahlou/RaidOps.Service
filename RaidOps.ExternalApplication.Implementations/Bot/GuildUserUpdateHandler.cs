using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Hosting.Gateway;
using RaidOps.Application.Contracts.Services;

namespace RaidOps.ExternalApplication.Implementations.Bot;

/// <summary>
/// Gateway event handler for the Discord <c>GUILD_MEMBER_UPDATE</c> event — fires whenever any
/// attribute of a guild member changes (roles, nickname, timeout, ...). No diffing is performed:
/// the Gateway cache is already the up-to-date source of truth, so every update simply tells the
/// affected user's connected clients (if any) to re-fetch <c>/user/me</c> via <see cref="IAuthNotifier"/>.
/// </summary>
/// <remarks>
/// NetCord registers gateway handlers as singletons. <see cref="IAuthNotifier"/> is resolved
/// from a fresh <see cref="IServiceScope"/> per event — same pattern as <see cref="GuildDeleteHandler"/>.
/// </remarks>
public class GuildUserUpdateHandler(
    IServiceScopeFactory scopeFactory,
    ILogger<GuildUserUpdateHandler> logger) : IGuildUserUpdateGatewayHandler
{
    /// <inheritdoc/>
    public async ValueTask HandleAsync(GuildUser arg)
    {
        try
        {
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("GUILD_MEMBER_UPDATE received for user {DiscordId} in guild {GuildId}.", arg.Id, arg.GuildId);

            await using var scope = scopeFactory.CreateAsyncScope();
            var authNotifier = scope.ServiceProvider.GetRequiredService<IAuthNotifier>();

            await authNotifier.NotifyDiscordDataChangedAsync(arg.Id.ToString());
        }
        catch (Exception ex)
        {
            // Never let a notification failure take the gateway connection down — worst case,
            // the user only gets the data update on their next reactive token refresh.
            logger.LogError(ex, "Failed to notify user {DiscordId} of a Discord data change.", arg.Id);
        }
    }
}
