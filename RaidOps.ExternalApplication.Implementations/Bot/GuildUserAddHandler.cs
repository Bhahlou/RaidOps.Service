using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Hosting.Gateway;

namespace RaidOps.ExternalApplication.Implementations.Bot;

/// <summary>
/// Gateway event handler for the Discord <c>GUILD_MEMBER_ADD</c> event — fires when a member
/// joins a guild, granting them a new guild-scoped access in RaidOps. See
/// <see cref="GuildUserChangeNotifier"/> for the shared notify logic.
/// </summary>
public class GuildUserAddHandler(
    IServiceScopeFactory scopeFactory,
    ILogger<GuildUserAddHandler> logger) : IGuildUserAddGatewayHandler
{
    /// <inheritdoc/>
    public ValueTask HandleAsync(GuildUser arg) =>
        GuildUserChangeNotifier.NotifyAsync(scopeFactory, logger, "GUILD_MEMBER_ADD", arg.Id, arg.GuildId);
}
