using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Raids.Events.Responses;

/// <summary>Minimal identity of a raid event — backs the raid detail page's breadcrumb, see <see cref="Queries.GetRaidEventSummaryQuery"/>.</summary>
public class RaidEventSummaryResponse
{
    /// <summary>Surrogate ID of the raid event.</summary>
    public required int Id { get; set; }

    /// <summary>Display name (e.g. "Split 1").</summary>
    public required string Name { get; set; }

    /// <summary>How attendance is determined for this event.</summary>
    public required SignupMode SignupMode { get; set; }

    /// <summary>
    /// The requesting user's own response for this event, or <c>null</c> if the event isn't in
    /// <see cref="Enums.SignupMode.Signup"/> mode or they haven't responded yet.
    /// </summary>
    public SignupStatus? MySignupStatus { get; set; }

    /// <summary>The character behind <see cref="MySignupStatus"/>, set for <see cref="SignupStatus.Accepted"/> and <see cref="SignupStatus.Tentative"/>.</summary>
    public int? MySignupCharacterId { get; set; }

    /// <summary>The spec behind <see cref="MySignupStatus"/>, set for <see cref="SignupStatus.Accepted"/> and <see cref="SignupStatus.Tentative"/>.</summary>
    public int? MySignupSpecId { get; set; }
}
