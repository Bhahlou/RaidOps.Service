namespace RaidOps.Application.Contracts.Guilds.Settings.Responses;

/// <summary>
/// Represents a Discord role that can be used for roster access control.
/// </summary>
public class DiscordRoleResponse
{
    /// <summary>Discord snowflake ID of the role.</summary>
    public required string Id { get; set; }

    /// <summary>Display name of the role.</summary>
    public required string Name { get; set; }

    /// <summary>
    /// Discord colour as a 24-bit RGB integer (0 = no colour / default white).
    /// Frontend can render as <c>#RRGGBB</c> using <c>color.ToString("X6")</c>.
    /// </summary>
    public int Color { get; set; }

    /// <summary>
    /// Role icon hash, or <c>null</c> if the role has no custom icon.
    /// Requires the guild to have the <c>ROLE_ICONS</c> feature (boost level 2+).
    /// </summary>
    public string? IconHash { get; set; }
}
