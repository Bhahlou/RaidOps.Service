using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Models.Raids;

namespace RaidOps.Application.Implementations.Raids.Services;

/// <inheritdoc/>
public class RaidSignupChangeNotifier(
    IRaidSignupAnnouncementService raidSignupAnnouncementService,
    IRaidSignupNotifier raidSignupNotifier) : IRaidSignupChangeNotifier
{
    /// <inheritdoc/>
    public async Task NotifyChangedAsync(RaidEvent raidEvent, CancellationToken cancellationToken = default)
    {
        await raidSignupAnnouncementService.PublishOrUpdateSignupCallAsync(raidEvent, cancellationToken);
        await raidSignupNotifier.NotifyRaidSignupChangedAsync(raidEvent.GuildBranchId, raidEvent.Id, cancellationToken);
    }
}
