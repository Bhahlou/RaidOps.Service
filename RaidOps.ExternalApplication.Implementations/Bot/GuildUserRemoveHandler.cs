using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;

namespace RaidOps.ExternalApplication.Implementations.Bot;

/// <summary>
/// Gateway event handler for the Discord <c>GUILD_MEMBER_REMOVE</c> event — fires when a member
/// leaves or is kicked/banned from a guild, which revokes their guild-scoped access in RaidOps.
/// See <see cref="GuildUserChangeNotifier"/> for the shared notify logic.
/// </summary>
public class GuildUserRemoveHandler(
    IServiceScopeFactory scopeFactory,
    ILogger<GuildUserRemoveHandler> logger) : IGuildUserRemoveGatewayHandler
{
    /// <inheritdoc/>
    public ValueTask HandleAsync(GuildUserRemoveEventArgs arg) =>
        GuildUserChangeNotifier.NotifyAsync(scopeFactory, logger, "GUILD_MEMBER_REMOVE", arg.User.Id, arg.GuildId);
}
