using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Events.Responses;

namespace RaidOps.Application.Contracts.Raids.Events.Queries;

/// <summary>
/// Returns a lightweight pick-list of the guild branch's raid events around now (draft and
/// published alike) — backs the create/edit raid dialogs' "extends the lockout of" selector. Unlike
/// <see cref="GetUpcomingPublishedRaidEventChoicesQuery"/> (guild-wide, published-only, backs the
/// Discord bot's unauthenticated autocomplete), this is branch-scoped, includes drafts, and requires
/// <see cref="Domain.Enums.GuildAccessLevel.Officer"/> access — a draft's name/date shouldn't leak to
/// a plain roster member just because they can query this list directly.
/// </summary>
public class GetRaidEventChoicesForBranchQuery : IQueryRequest<List<RaidEventChoiceResponse>>
{
    /// <summary>Discord snowflake ID of the guild this event belongs to. Set by the controller, not from the request body.</summary>
    public required string GuildId { get; set; }

    /// <summary>Surrogate ID of the guild branch to list raid events for. Set by the controller from the route, not from the request body.</summary>
    public required int GuildBranchId { get; set; }

    /// <summary>Discord snowflake ID of the requesting user. Set by the controller, not from the request body.</summary>
    public required string RequesterDiscordId { get; set; }

    /// <summary>
    /// UTC timestamp of the raid event being created/edited — candidates are narrowed to the branch's
    /// lockout window covering this instant (same window <see cref="GetGuildBranchLockoutWeekQuery"/>
    /// would report for "now", just centered on this date instead), since a raid outside that window
    /// couldn't share a lockout with it anyway. Falls back to a plain ±60-day range around this date
    /// when the branch has no region/<c>WeeklyLockoutSchedule</c> configured to compute a window from.
    /// </summary>
    public required DateTime AroundStartsAtUtc { get; set; }
}
