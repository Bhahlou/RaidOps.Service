using Microsoft.AspNetCore.SignalR;
using RaidOps.Application.Contracts.Services;

namespace RaidOps.API.Hubs;

/// <summary>
/// SignalR-backed implementation of <see cref="IAuthNotifier"/>. Lives in the API layer (rather
/// than alongside its interface in Application.Contracts) because it depends on
/// <see cref="IHubContext{THub}"/>/<see cref="AuthHub"/>, which are ASP.NET Core hosting concerns
/// the Application layer must stay ignorant of.
/// </summary>
public class AuthNotifier(IHubContext<AuthHub> hubContext) : IAuthNotifier
{
    /// <inheritdoc/>
    public Task NotifyDiscordDataChangedAsync(string discordId, CancellationToken cancellationToken = default)
        => hubContext.Clients.User(discordId).SendAsync("DiscordDataChanged", cancellationToken: cancellationToken);
}
