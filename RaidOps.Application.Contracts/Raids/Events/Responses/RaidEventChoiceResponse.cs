namespace RaidOps.Application.Contracts.Raids.Events.Responses;

/// <summary>
/// One upcoming published raid event, as a lightweight pick-list entry for the Discord bot's
/// <c>/raid invite</c> subcommand autocomplete — see <see cref="Queries.GetUpcomingPublishedRaidEventChoicesQuery"/>.
/// </summary>
public class RaidEventChoiceResponse
{
    /// <summary>Surrogate ID of the raid event.</summary>
    public required int Id { get; set; }

    /// <summary>Surrogate ID of the guild branch this event belongs to.</summary>
    public required int GuildBranchId { get; set; }

    /// <summary>Display name (e.g. "Split 1").</summary>
    public required string Name { get; set; }

    /// <summary>
    /// Local wall-clock start time in the guild's configured timezone (falls back to UTC if the
    /// guild hasn't configured one) — pre-converted so the autocomplete list reads naturally
    /// without the bot needing to know the guild's timezone itself.
    /// </summary>
    public required DateTime StartsAtLocal { get; set; }

    /// <summary>Display name of the WoW game-version branch this event belongs to (e.g. "Classic Era").</summary>
    public required string BranchName { get; set; }
}
