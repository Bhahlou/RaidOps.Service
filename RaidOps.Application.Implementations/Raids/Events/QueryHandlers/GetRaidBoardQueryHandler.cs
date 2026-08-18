using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Events.Queries;
using RaidOps.Application.Contracts.Raids.Events.Responses;
using RaidOps.Application.Contracts.Raids.Zones.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Helpers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Raids;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Events.QueryHandlers;

/// <summary>
/// Handles <see cref="GetRaidBoardQuery"/> by returning every raid event of a guild branch within
/// a date range, with target zones, slot assignments (each with the assigned player's resolved
/// availability on the event's guild-local date), and the set of roster players — assigned or not
/// — whose declared availability would reject an assignment to that event, so the front end can
/// mark a drop target as blocked while a drag is still in progress. Availability is resolved from
/// two bulk reads scoped to the branch's roster player set (rather than one query per member), and
/// correctly covers every declaration scope (Global and branch-specific alike) since the roster
/// player set is already known up front. Does not materialize series occurrences itself — the caller runs
/// <c>MaterializeRaidSeriesOccurrencesCommand</c> for the same range first.
/// </summary>
public class GetRaidBoardQueryHandler(
    IGuildAccessService guildAccessService,
    IGuildsRepository guildsRepository,
    IRaidEventRepository raidEventRepository,
    IGuildMembershipRepository guildMembershipRepository,
    IRaidBoardEnrichmentDataLoader enrichmentDataLoader) : IQueryHandlerAsync<GetRaidBoardQuery, RaidBoardResponse>
{
    /// <inheritdoc/>
    public async Task<Result<RaidBoardResponse>> HandleAsync(GetRaidBoardQuery query, CancellationToken cancellationToken)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(query.RequesterDiscordId, query.GuildId, query.GuildBranchId, cancellationToken);
        if (accessLevel < GuildAccessLevel.Roster)
            return Result<RaidBoardResponse>.Fail(ResponseDetail.Forbidden, "User is not on this guild branch's roster.");

        if (query.RangeEnd < query.RangeStart)
            return Result<RaidBoardResponse>.Fail(ResponseDetail.InvalidRequest, "RangeEnd must be on or after RangeStart.");

        var guild = await guildsRepository.GetByIdAsync(query.GuildId, cancellationToken);
        if (guild == null)
            return Result<RaidBoardResponse>.Fail(ResponseDetail.GuildNotFound, $"Guild '{query.GuildId}' does not exist.");

        var rangeStartUtc = GuildTimeHelper.FromGuildLocal(query.RangeStart.ToDateTime(TimeOnly.MinValue), guild.Timezone);
        var rangeEndUtc = GuildTimeHelper.FromGuildLocal(query.RangeEnd.ToDateTime(new TimeOnly(23, 59, 59)), guild.Timezone);

        var events = await raidEventRepository.GetForGuildBranchInRangeAsync(query.GuildBranchId, rangeStartUtc, rangeEndUtc, cancellationToken);

        // Draft DefaultPresent events stay private to officers preparing the raid — a Roster
        // requester only ever sees the "official" schedule that's already been published. Signup
        // events are the exception: the whole point of Signup mode is gathering responses before
        // the raid is built (the Discord signup call is already posted from creation, with no
        // Published gate of its own — see SetMyRaidSignupCommandHandler), so hiding a draft Signup
        // event from the web board would leave roster members able to respond on Discord but blind
        // to that same event here.
        if (accessLevel < GuildAccessLevel.Officer)
            events = [.. events.Where(e => e.PublicationStatus == RaidPublicationStatus.Published || e.SignupMode == SignupMode.Signup)];

        var rosterMemberships = await guildMembershipRepository.GetByGuildBranchIdAsync(query.GuildBranchId, cancellationToken);
        var rosterPlayerIds = rosterMemberships.Select(m => m.Character.UserDiscordId).Distinct().ToList();

        var enrichment = await enrichmentDataLoader.LoadAsync(events, rosterPlayerIds, query.GuildId, query.GuildBranchId, query.RangeStart, query.RangeEnd, cancellationToken);

        var context = new BoardMappingContext(guild, rosterPlayerIds, enrichment.PlayersById, enrichment.AvailabilityLookup, enrichment.SignupsByEvent, enrichment.RaidSpecsByCharacter, query.RequesterDiscordId);
        var response = new RaidBoardResponse
        {
            Events = [.. events.Select(e => MapEvent(e, context))],
        };

        return Result<RaidBoardResponse>.Ok(response);
    }

    /// <summary>Bundles the per-request state shared across every event/assignment mapped for one board response.</summary>
    private sealed record BoardMappingContext(
        Guild Guild,
        List<string> RosterPlayerIds,
        Dictionary<string, User> PlayersById,
        IRaidAvailabilityLookup AvailabilityLookup,
        Dictionary<int, Dictionary<string, RaidSignup>> SignupsByEvent,
        Dictionary<int, List<CharacterRaidSpec>> RaidSpecsByCharacter,
        string RequesterDiscordId);

    private static RaidEventResponse MapEvent(RaidEvent raidEvent, BoardMappingContext ctx)
    {
        var localDateTime = GuildTimeHelper.ToGuildLocalDateTime(raidEvent.StartsAtUtc, ctx.Guild.Timezone);
        var localDate = DateOnly.FromDateTime(localDateTime);
        var localTime = TimeOnly.FromDateTime(localDateTime);

        var eventSignups = ctx.SignupsByEvent.GetValueOrDefault(raidEvent.Id, []);

        var ineligiblePlayerIds = raidEvent.SignupMode == SignupMode.Signup
            ? [.. ctx.RosterPlayerIds.Where(playerId => eventSignups.GetValueOrDefault(playerId)?.Status != SignupStatus.Accepted)]
            : ctx.RosterPlayerIds.Where(playerId => ctx.AvailabilityLookup.IsUnavailableAt(playerId, localDate, localTime)).ToList();

        var acceptedCharacterIdsByPlayer = raidEvent.SignupMode == SignupMode.Signup
            ? eventSignups
                .Where(kv => kv.Value.Status == SignupStatus.Accepted && kv.Value.CharacterId != null)
                .ToDictionary(kv => kv.Key, kv => kv.Value.CharacterId!.Value)
            : [];

        return new RaidEventResponse
        {
            Id = raidEvent.Id,
            RaidSeriesId = raidEvent.RaidSeriesId,
            Name = raidEvent.Name,
            BranchId = raidEvent.GuildBranch.BranchId,
            BranchName = raidEvent.GuildBranch.Branch.Name,
            StartsAtUtc = raidEvent.StartsAtUtc,
            GroupCount = raidEvent.GroupCount,
            SlotsPerGroup = raidEvent.SlotsPerGroup,
            SignupMode = raidEvent.SignupMode,
            Status = raidEvent.Status,
            PublicationStatus = raidEvent.PublicationStatus,
            PublishedAt = raidEvent.PublishedAt,
            PublishedByDiscordId = raidEvent.PublishedByDiscordId,
            RaidZones = [.. raidEvent.TargetZones.Select(z => new RaidZoneRefResponse
            {
                Id = z.RaidZoneId,
                Name = z.RaidZone.Name,
                ShortCode = z.RaidZone.ShortCode,
            })],
            Assignments = [.. raidEvent.Assignments.Select(a => MapAssignment(a, localDate, eventSignups, ctx))],
            IneligiblePlayerDiscordIds = ineligiblePlayerIds,
            MySignupStatus = eventSignups.GetValueOrDefault(ctx.RequesterDiscordId)?.Status,
            MySignupCharacterId = eventSignups.GetValueOrDefault(ctx.RequesterDiscordId)?.CharacterId,
            MySignupSpecId = eventSignups.GetValueOrDefault(ctx.RequesterDiscordId)?.SpecId,
            AcceptedCharacterIdsByPlayerDiscordId = acceptedCharacterIdsByPlayer,
            DedicatedAnnouncementChannelId = raidEvent.DedicatedAnnouncementChannelId,
            DedicatedAnnouncementChannelIsBotOwned = raidEvent.DedicatedAnnouncementChannelIsBotOwned,
        };
    }

    private static RaidSlotAssignmentResponse MapAssignment(RaidSlotAssignment assignment, DateOnly eventLocalDate, Dictionary<string, RaidSignup> eventSignups, BoardMappingContext ctx)
    {
        ctx.PlayersById.TryGetValue(assignment.AssignedPlayerDiscordId, out var player);

        var availabilityStatus = ctx.AvailabilityLookup.ResolveStatus(assignment.AssignedPlayerDiscordId, eventLocalDate);
        var characterRaidSpecs = ctx.RaidSpecsByCharacter.GetValueOrDefault(assignment.CharacterId, []);

        return new RaidSlotAssignmentResponse
        {
            GroupNumber = assignment.GroupNumber,
            SlotNumber = assignment.SlotNumber,
            CharacterId = assignment.CharacterId,
            CharacterName = assignment.Character.Name,
            ClassId = assignment.Character.ClassId,
            ClassColor = "#" + assignment.Character.Class.Color,
            PlayerDiscordId = assignment.AssignedPlayerDiscordId,
            PlayerName = player?.Name,
            AvailabilityStatus = availabilityStatus,
            SignupStatus = eventSignups.GetValueOrDefault(assignment.AssignedPlayerDiscordId)?.Status,
            Spec = MapSpecRef(assignment.Spec),
            AvailableSpecs = [.. characterRaidSpecs.Select(rs => MapSpecRef(rs.Spec))],
        };
    }

    private static RaidSpecRefResponse MapSpecRef(Spec spec) => new()
    {
        Id = spec.Id,
        Name = spec.Name,
        IconUrl = spec.IconUrl,
    };
}
