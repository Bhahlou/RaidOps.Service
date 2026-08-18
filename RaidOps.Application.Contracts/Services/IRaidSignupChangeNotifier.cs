using RaidOps.Domain.Models.Raids;

namespace RaidOps.Application.Contracts.Services;

/// <summary>
/// Propagates a signup response change through both channels that need to know about it: the
/// standing Discord signup-call embed (<see cref="IRaidSignupAnnouncementService"/>) and the
/// live web board (<see cref="IRaidSignupNotifier"/>).
/// </summary>
public interface IRaidSignupChangeNotifier
{
    Task NotifyChangedAsync(RaidEvent raidEvent, CancellationToken cancellationToken = default);
}
