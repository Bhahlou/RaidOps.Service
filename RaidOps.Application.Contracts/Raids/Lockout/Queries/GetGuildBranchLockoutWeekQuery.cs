using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Lockout.Responses;

namespace RaidOps.Application.Contracts.Raids.Lockout.Queries;

/// <summary>
/// Query for the current weekly raid-lockout window (region reset to region reset) of a guild
/// branch — used by the raid builder to default its date-range navigator to "this lockout week"
/// instead of an arbitrary calendar week. The requesting user must hold at least
/// <see cref="Domain.Enums.GuildAccessLevel.Roster"/> access on <see cref="GuildId"/>.
/// </summary>
public class GetGuildBranchLockoutWeekQuery : IQueryRequest<GuildBranchLockoutWeekResponse>
{
    /// <summary>The Discord snowflake ID of the guild. Set by the controller, not from the request body.</summary>
    public required string GuildId { get; set; }

    /// <summary>Surrogate ID of the guild branch. Set by the controller, not from the request body.</summary>
    public required int GuildBranchId { get; set; }

    /// <summary>The Discord snowflake ID of the requesting user. Set by the controller, not from the request body.</summary>
    public required string RequesterDiscordId { get; set; }
}
