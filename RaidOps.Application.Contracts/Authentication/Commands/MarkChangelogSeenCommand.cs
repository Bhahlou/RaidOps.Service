using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Authentication.Commands;

/// <summary>
/// Command that records the changelog entries a user has just acknowledged (typically every
/// entry inside a "what's new" category the user expanded), so "what's new" state can be synced
/// across devices instead of living in browser storage.
/// </summary>
public class MarkChangelogSeenCommand : ICommandRequest
{
    /// <summary>The Discord snowflake ID of the user acknowledging the entries. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>The ids of the changelog entries the user has now seen.</summary>
    public required List<string> EntryIds { get; set; }
}
