using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Events.Queries;
using RaidOps.Application.Contracts.Raids.Events.Responses;
using RaidOps.Application.Contracts.Raids.Zones.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Helpers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Calendar;
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
    IAvailabilityRepository availabilityRepository,
    IAvailabilityResolutionService availabilityResolutionService,
    IUsersRepository usersRepository,
    ICharacterRepository characterRepository) : IQueryHandlerAsync<GetRaidBoardQuery, RaidBoardResponse>
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

        // Draft events stay private to officers preparing the raid — a Roster requester only ever
        // sees the "official" schedule that's already been published.
        if (accessLevel < GuildAccessLevel.Officer)
            events = [.. events.Where(e => e.PublicationStatus == RaidPublicationStatus.Published)];

        var rosterMemberships = await guildMembershipRepository.GetByGuildBranchIdAsync(query.GuildBranchId, cancellationToken);
        var rosterPlayerIds = rosterMemberships.Select(m => m.Character.UserDiscordId).Distinct().ToList();

        var exceptions = await availabilityRepository.GetExceptionsOverlappingForUsersAsync(rosterPlayerIds, query.RangeStart, query.RangeEnd, cancellationToken);
        var patterns = await availabilityRepository.GetPatternsForUsersAsync(rosterPlayerIds, cancellationToken);

        var assignedPlayerIds = events.SelectMany(e => e.Assignments).Select(a => a.AssignedPlayerDiscordId).Distinct().ToList();
        var players = await usersRepository.FindAsync(u => assignedPlayerIds.Contains(u.DiscordId), cancellationToken);
        var playersById = players.ToDictionary(u => u.DiscordId);

        var assignedCharacterIds = events.SelectMany(e => e.Assignments).Select(a => a.CharacterId).Distinct().ToList();
        var raidSpecs = await characterRepository.GetRaidSpecsForCharactersAsync(assignedCharacterIds, cancellationToken);
        var raidSpecsByCharacter = raidSpecs
            .GroupBy(rs => rs.CharacterId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var response = new RaidBoardResponse
        {
            Events = [.. events.Select(e => MapEvent(e, guild, query.GuildId, query.GuildBranchId, playersById, rosterPlayerIds, exceptions, patterns, raidSpecsByCharacter))],
        };

        return Result<RaidBoardResponse>.Ok(response);
    }

    private RaidEventResponse MapEvent(
        RaidEvent raidEvent,
        Guild guild,
        string guildId,
        int guildBranchId,
        Dictionary<string, User> playersById,
        List<string> rosterPlayerIds,
        List<AvailabilityDeclaration> guildExceptions,
        List<RecurringAvailabilityPattern> guildPatterns,
        Dictionary<int, List<CharacterRaidSpec>> raidSpecsByCharacter)
    {
        var localDateTime = GuildTimeHelper.ToGuildLocalDateTime(raidEvent.StartsAtUtc, guild.Timezone);
        var localDate = DateOnly.FromDateTime(localDateTime);
        var localTime = TimeOnly.FromDateTime(localDateTime);

        var absentPlayerIds = rosterPlayerIds
            .Where(playerId => IsPlayerAbsent(playerId, localDate, localTime, guildId, guildBranchId, guildExceptions, guildPatterns))
            .ToList();

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
            Assignments = [.. raidEvent.Assignments.Select(a => MapAssignment(a, localDate, guildId, guildBranchId, playersById, guildExceptions, guildPatterns, raidSpecsByCharacter))],
            AbsentPlayerDiscordIds = absentPlayerIds,
        };
    }

    private RaidSlotAssignmentResponse MapAssignment(
        RaidSlotAssignment assignment,
        DateOnly eventLocalDate,
        string guildId,
        int guildBranchId,
        Dictionary<string, User> playersById,
        List<AvailabilityDeclaration> guildExceptions,
        List<RecurringAvailabilityPattern> guildPatterns,
        Dictionary<int, List<CharacterRaidSpec>> raidSpecsByCharacter)
    {
        playersById.TryGetValue(assignment.AssignedPlayerDiscordId, out var player);

        var memberExceptions = guildExceptions.Where(e => e.UserDiscordId == assignment.AssignedPlayerDiscordId).ToList();
        var memberPatterns = guildPatterns.Where(p => p.UserDiscordId == assignment.AssignedPlayerDiscordId).ToList();
        var resolved = availabilityResolutionService.ResolveForScope(eventLocalDate, eventLocalDate, memberExceptions, memberPatterns, guildId, guildBranchId);
        var availabilityStatus = resolved.Count > 0 ? resolved[0].Status : DayAvailabilityStatus.Available;

        var characterRaidSpecs = raidSpecsByCharacter.GetValueOrDefault(assignment.CharacterId, []);

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

    /// <summary>
    /// Mirrors <c>AssignCharacterToSlotCommandHandler.CheckDeclaredAbsenceAsync</c>'s blocking rule
    /// exactly: hard <see cref="DayAvailabilityStatus.Absent"/>, or <see cref="DayAvailabilityStatus.Partial"/>
    /// whose declared window doesn't cover the event's local start time.
    /// </summary>
    private bool IsPlayerAbsent(
        string playerId,
        DateOnly eventLocalDate,
        TimeOnly eventLocalTime,
        string guildId,
        int guildBranchId,
        List<AvailabilityDeclaration> guildExceptions,
        List<RecurringAvailabilityPattern> guildPatterns)
    {
        var memberExceptions = guildExceptions.Where(e => e.UserDiscordId == playerId).ToList();
        var memberPatterns = guildPatterns.Where(p => p.UserDiscordId == playerId).ToList();
        var resolved = availabilityResolutionService.ResolveForScope(eventLocalDate, eventLocalDate, memberExceptions, memberPatterns, guildId, guildBranchId);
        if (resolved.Count == 0)
            return false;

        var day = resolved[0];
        if (day.Status == DayAvailabilityStatus.Absent)
            return true;

        if (day.Status == DayAvailabilityStatus.Partial)
        {
            var withinWindow =
                (day.AvailableFrom == null || eventLocalTime >= day.AvailableFrom.Value) &&
                (day.AvailableUntil == null || eventLocalTime <= day.AvailableUntil.Value);
            return !withinWindow;
        }

        return false;
    }
}
