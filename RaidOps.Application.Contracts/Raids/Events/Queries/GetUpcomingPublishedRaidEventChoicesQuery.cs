using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Events.Responses;

namespace RaidOps.Application.Contracts.Raids.Events.Queries;

/// <summary>
/// Returns the guild's upcoming published raid events, across every branch — backs the Discord
/// bot's <c>/raid invite</c> subcommand autocomplete, which needs to list "which raid" independently
/// of any branch context (Discord has no notion of a RaidOps branch). Deliberately unauthenticated
/// beyond guild scoping: the same events are already visible to any roster member on the site, and
/// the slash-command's actual action (<see cref="Commands.TriggerRaidGroupingCommand"/>) enforces
/// Officer access on its own.
/// </summary>
public class GetUpcomingPublishedRaidEventChoicesQuery : IQueryRequest<List<RaidEventChoiceResponse>>
{
    /// <summary>Discord snowflake ID of the guild whose upcoming raids to list.</summary>
    public required string GuildId { get; set; }
}
