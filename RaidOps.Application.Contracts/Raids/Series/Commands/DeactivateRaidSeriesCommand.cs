using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Raids.Series.Commands;

/// <summary>
/// Stops future materialization of a recurring raid template, without altering the occurrences it
/// already produced — unless <see cref="DeleteEmptyOccurrences"/> is set, which additionally bulk
/// deletes the ones still empty and unpublished (draft, no assignments). Anything published or with
/// roster history is always left untouched. The requesting user must hold
/// <see cref="Domain.Enums.GuildAccessLevel.Officer"/> access on <see cref="GuildId"/>.
/// </summary>
public class DeactivateRaidSeriesCommand : ICommandRequest
{
    /// <summary>Discord snowflake ID of the guild this series belongs to. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>Discord snowflake ID of the officer deactivating this series. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>Surrogate ID of the guild branch this series belongs to. Set by the controller from the route, not from the request body.</summary>
    public int GuildBranchId { get; set; }

    /// <summary>ID of the series to deactivate. Set by the controller from the route, not from the request body.</summary>
    public int SeriesId { get; set; }

    /// <summary>Also bulk deletes the series' already-materialized draft, zero-assignment occurrences.</summary>
    public bool DeleteEmptyOccurrences { get; set; }
}
