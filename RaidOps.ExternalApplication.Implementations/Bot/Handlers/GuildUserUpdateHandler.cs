using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Hosting.Gateway;

namespace RaidOps.ExternalApplication.Implementations.Bot.Handlers;

/// <summary>
/// Gateway event handler for the Discord <c>GUILD_MEMBER_UPDATE</c> event — fires whenever any
/// attribute of a guild member changes (roles, nickname, timeout, ...), which may affect their
/// RaidOps permissions. See <see cref="GuildUserChangeNotifier"/> for the shared notify logic.
/// </summary>
public class GuildUserUpdateHandler(
    IServiceScopeFactory scopeFactory,
    ILogger<GuildUserUpdateHandler> logger) : IGuildUserUpdateGatewayHandler
{
    /// <inheritdoc/>
    public ValueTask HandleAsync(GuildUser arg) =>
        GuildUserChangeNotifier.NotifyAsync(scopeFactory, logger, "GUILD_MEMBER_UPDATE", arg.Id, arg.GuildId);
}
