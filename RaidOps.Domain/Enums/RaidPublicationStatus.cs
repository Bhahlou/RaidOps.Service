namespace RaidOps.Domain.Enums;

/// <summary>
/// Draft/published status of a <c>RaidEvent</c>, orthogonal to its <see cref="RaidEventStatus"/>
/// lifecycle. A raid is prepared privately as a draft and only becomes visible to regular roster
/// members once an officer explicitly publishes it.
/// </summary>
public enum RaidPublicationStatus
{
    /// <summary>Being prepared by officers — hidden from non-officer roster members and excluded from the "unassigned members" computation.</summary>
    Draft = 0,

    /// <summary>Officially published — visible to every roster member and counted toward the "unassigned members" computation.</summary>
    Published = 1,
}
