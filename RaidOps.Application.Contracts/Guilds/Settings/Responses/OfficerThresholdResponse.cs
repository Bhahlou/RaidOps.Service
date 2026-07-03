namespace RaidOps.Application.Contracts.Guilds.Settings.Responses;

/// <summary>
/// DTO returned by <see cref="Queries.GetOfficerThresholdQuery"/>.
/// </summary>
public class OfficerThresholdResponse
{
    /// <summary>
    /// Discord snowflake ID of the minimum role that grants Officer access, or <c>null</c> if
    /// not configured yet — only Discord Administrator/owner has Officer access in that case.
    /// </summary>
    public string? MinOfficerRoleId { get; set; }
}
