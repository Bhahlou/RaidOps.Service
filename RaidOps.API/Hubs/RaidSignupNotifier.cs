using Microsoft.AspNetCore.SignalR;
using RaidOps.Application.Contracts.Services;

namespace RaidOps.API.Hubs;

/// <summary>
/// SignalR-backed implementation of <see cref="IRaidSignupNotifier"/>. Lives in the API layer
/// (rather than alongside its interface in Application.Contracts) because it depends on
/// <see cref="IHubContext{THub}"/>/<see cref="RaidSignupHub"/>, which are ASP.NET Core hosting
/// concerns the Application layer must stay ignorant of.
/// </summary>
public class RaidSignupNotifier(IHubContext<RaidSignupHub> hubContext) : IRaidSignupNotifier
{
    /// <inheritdoc/>
    public Task NotifyRaidSignupChangedAsync(int guildBranchId, int eventId, CancellationToken cancellationToken = default)
        => hubContext.Clients
            .Group(RaidSignupHub.GroupName(guildBranchId, eventId))
            .SendAsync("RaidSignupChanged", eventId, cancellationToken: cancellationToken);
}
