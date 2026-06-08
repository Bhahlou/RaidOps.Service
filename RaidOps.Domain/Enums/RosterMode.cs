namespace RaidOps.Domain.Enums;

/// <summary>
/// Controls who is allowed to join a guild's roster.
/// </summary>
public enum RosterMode
{
    /// <summary>Any authenticated user can join the roster without restriction.</summary>
    Open = 1,

    /// <summary>Only users holding at least one of the guild's allowed Discord roles can join the roster.</summary>
    DiscordRoleOnly = 2
}
