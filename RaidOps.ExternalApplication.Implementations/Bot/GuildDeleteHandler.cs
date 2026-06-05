using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Commands;

namespace RaidOps.ExternalApplication.Implementations.Bot;

/// <summary>
/// Gateway event handler for the Discord <c>GUILD_DELETE</c> event.
/// When the bot is removed from a guild (as opposed to a transient unavailability),
/// dispatches <see cref="UnregisterGuildCommand"/> to mark the guild as unregistered in RaidOps.
/// </summary>
/// <remarks>
/// NetCord registers gateway handlers as singletons.
/// <see cref="ICommandDispatcher"/> is scoped, so a fresh <see cref="IServiceScope"/>
/// is created per event to avoid consuming a scoped service from a singleton.
/// </remarks>
public class GuildDeleteHandler(
    IServiceScopeFactory scopeFactory,
    ILogger<GuildDeleteHandler> logger) : IGuildDeleteGatewayHandler
{
    /// <summary>
    /// Handles the <c>GUILD_DELETE</c> Gateway event.
    /// Ignores transient outages (<c>IsUnavailable = true</c>);
    /// dispatches <see cref="UnregisterGuildCommand"/> otherwise.
    /// </summary>
    /// <param name="args">The event arguments provided by the Discord Gateway.</param>
    public async ValueTask HandleAsync(GuildDeleteEventArgs arg)
    {
        // IsUnavailable = true means a Discord outage, not a bot removal — ignore.
        if (arg.IsUnavailable)
        {
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("Guild {GuildId} became unavailable (outage), skipping unregister.", arg.GuildId);
            return;
        }

        logger.LogWarning("Bot removed from guild {GuildId}. Dispatching unregister…", arg.GuildId);

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();

            var result = await dispatcher.DispatchAsync(new UnregisterGuildCommand
            {
                GuildId = arg.GuildId.ToString()
            });

            if (result.IsFailed)
                logger.LogError("Failed to unregister guild {GuildId}: {Error}", arg.GuildId, result.Error);
            else if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("Guild {GuildId} successfully unregistered.", arg.GuildId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while unregistering guild {GuildId}.", arg.GuildId);
        }
    }
}
