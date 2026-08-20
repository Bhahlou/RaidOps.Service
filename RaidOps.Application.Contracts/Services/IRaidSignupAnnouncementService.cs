using RaidOps.Domain.Models.Raids;

namespace RaidOps.Application.Contracts.Services;

/// <summary>
/// Maintains the standing signup-call Discord embed (Accept/Tentative/Decline buttons) for a
/// published raid event in <see cref="RaidOps.Domain.Enums.SignupMode.Signup"/> mode — the
/// counterpart to <see cref="IRaidCompositionAnnouncementService"/> for the "who's coming" concern
/// rather than "who's assigned." Kept as a separate service since the two are otherwise orthogonal:
/// composition keeps meaning exactly what it means today regardless of signup mode. Every method
/// resolves its own setting and no-ops silently if disabled/unconfigured or if the Discord call
/// fails — same contract as <see cref="IRaidCompositionAnnouncementService"/>, this must never fail
/// the caller's own command.
/// </summary>
public interface IRaidSignupAnnouncementService
{
    /// <summary>
    /// Posts the standing signup-call embed if it's never been posted for this event yet (even with
    /// zero responses so far), or edits it in place to reflect current tallies otherwise.
    /// </summary>
    Task PublishOrUpdateSignupCallAsync(RaidEvent raidEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the standing signup-call message, if one was ever posted for this event — called
    /// when the event itself is deleted. No-ops if none was ever posted.
    /// </summary>
    Task DeleteSignupCallAsync(RaidEvent raidEvent, CancellationToken cancellationToken = default);
}
