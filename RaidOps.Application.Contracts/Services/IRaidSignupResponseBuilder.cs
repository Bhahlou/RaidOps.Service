using RaidOps.Application.Contracts.Raids.Signups.Responses;
using RaidOps.Domain.Models.Raids;

namespace RaidOps.Application.Contracts.Services;

/// <summary>
/// Builds one <see cref="RaidSignupResponse"/> per roster member of a raid event's guild branch —
/// every member gets an entry even without a <c>RaidSignup</c> row yet (no response), so the
/// signup-call embed/board can show who hasn't answered at all, not just who has.
/// </summary>
public interface IRaidSignupResponseBuilder
{
    Task<List<RaidSignupResponse>> BuildAsync(RaidEvent raidEvent, CancellationToken cancellationToken = default);
}
