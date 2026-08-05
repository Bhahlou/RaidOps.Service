using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using RaidOps.Application.Contracts.Services;

namespace RaidOps.ExternalApplication.Implementations.Bot;

/// <summary>
/// Gateway event handler for the Discord <c>GUILD_MEMBER_REMOVE</c> event — fires when a member
/// leaves or is kicked/banned from a guild, which revokes their guild-scoped access in RaidOps.
/// Same push-and-refetch approach as <see cref="GuildUserUpdateHandler"/>: no diffing, just tells
/// the affected user's connected clients (if any) to re-fetch <c>/user/me</c> via <see cref="IAuthNotifier"/>
/// so the guild disappears from their session immediately instead of after up to 15 minutes.
/// </summary>
/// <remarks>
/// NetCord registers gateway handlers as singletons. <see cref="IAuthNotifier"/> is resolved
/// from a fresh <see cref="IServiceScope"/> per event — same pattern as <see cref="GuildDeleteHandler"/>.
/// </remarks>
public class GuildUserRemoveHandler(
    IServiceScopeFactory scopeFactory,
    ILogger<GuildUserRemoveHandler> logger) : IGuildUserRemoveGatewayHandler
{
    /// <inheritdoc/>
    public async ValueTask HandleAsync(GuildUserRemoveEventArgs arg)
    {
        try
        {
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("GUILD_MEMBER_REMOVE received for user {DiscordId} in guild {GuildId}.", arg.User.Id, arg.GuildId);

            await using var scope = scopeFactory.CreateAsyncScope();
            var authNotifier = scope.ServiceProvider.GetRequiredService<IAuthNotifier>();

            await authNotifier.NotifyDiscordDataChangedAsync(arg.User.Id.ToString());
        }
        catch (Exception ex)
        {
            // Never let a notification failure take the gateway connection down — worst case,
            // the user only gets the data update on their next reactive token refresh.
            logger.LogError(ex, "Failed to notify user {DiscordId} of a Discord data change.", arg.User.Id);
        }
    }
}
