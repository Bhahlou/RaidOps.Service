using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Raids.Events.Commands;

/// <summary>
/// Idempotently materializes concrete <c>RaidEvent</c> occurrences for every active
/// <c>RaidSeries</c> of a guild that fall within a date range. Safe to call repeatedly for
/// overlapping ranges — an occurrence already materialized for a given series/date is never
/// duplicated. Run automatically whenever the raid board is opened over a range, since there is
/// no background job in this codebase to materialize on a schedule. Only realizes an already-
/// defined recurrence plan, so the requesting user only needs
/// <see cref="Domain.Enums.GuildAccessLevel.Roster"/> access on <see cref="GuildId"/>.
/// </summary>
public class MaterializeRaidSeriesOccurrencesCommand : ICommandRequest
{
    /// <summary>Discord snowflake ID of the guild to materialize occurrences for. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>Discord snowflake ID of the requesting user. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>First local date (inclusive) to materialize occurrences for.</summary>
    public required DateOnly RangeStart { get; set; }

    /// <summary>Last local date (inclusive) to materialize occurrences for.</summary>
    public required DateOnly RangeEnd { get; set; }
}
