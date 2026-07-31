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
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Events.QueryHandlers;

/// <summary>
/// Handles <see cref="GetRaidBoardQuery"/> by returning every raid event of a guild branch within
/// a date range, with target zones, slot assignments, and each assigned player's resolved
/// availability on the event's guild-local date. Availability is resolved from two guild-wide bulk
/// reads (rather than one query per member) so the board scales with event/assignment count, not
/// roster size — a known limitation of that bulk read is that it only sees branch-scoped
/// declarations, not Global ones (see <see cref="IAvailabilityRepository.GetExceptionsOverlappingForGuildAsync"/>).
/// Does not materialize series occurrences itself — the caller runs
/// <c>MaterializeRaidSeriesOccurrencesCommand</c> for the same range first.
/// </summary>
public class GetRaidBoardQueryHandler(
    IGuildAccessService guildAccessService,
    IGuildsRepository guildsRepository,
    IRaidEventRepository raidEventRepository,
    IAvailabilityRepository availabilityRepository,
    IAvailabilityResolutionService availabilityResolutionService,
    IUsersRepository usersRepository) : IQueryHandlerAsync<GetRaidBoardQuery, RaidBoardResponse>
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

        var exceptions = await availabilityRepository.GetExceptionsOverlappingForGuildAsync(query.GuildId, query.RangeStart, query.RangeEnd, cancellationToken);
        var patterns = await availabilityRepository.GetPatternsForGuildAsync(query.GuildId, cancellationToken);

        var assignedPlayerIds = events.SelectMany(e => e.Assignments).Select(a => a.AssignedPlayerDiscordId).Distinct().ToList();
        var players = await usersRepository.FindAsync(u => assignedPlayerIds.Contains(u.DiscordId), cancellationToken);
        var playersById = players.ToDictionary(u => u.DiscordId);

        var response = new RaidBoardResponse
        {
            Events = [.. events.Select(e => MapEvent(e, guild, query.GuildId, query.GuildBranchId, playersById, exceptions, patterns))],
        };

        return Result<RaidBoardResponse>.Ok(response);
    }

    private RaidEventResponse MapEvent(
        RaidEvent raidEvent,
        Guild guild,
        string guildId,
        int guildBranchId,
        Dictionary<string, User> playersById,
        List<AvailabilityDeclaration> guildExceptions,
        List<RecurringAvailabilityPattern> guildPatterns)
    {
        var localDate = GuildTimeHelper.ToGuildLocalDate(raidEvent.StartsAtUtc, guild.Timezone);

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
            Assignments = [.. raidEvent.Assignments.Select(a => MapAssignment(a, localDate, guildId, guildBranchId, playersById, guildExceptions, guildPatterns))],
        };
    }

    private RaidSlotAssignmentResponse MapAssignment(
        RaidSlotAssignment assignment,
        DateOnly eventLocalDate,
        string guildId,
        int guildBranchId,
        Dictionary<string, User> playersById,
        List<AvailabilityDeclaration> guildExceptions,
        List<RecurringAvailabilityPattern> guildPatterns)
    {
        playersById.TryGetValue(assignment.AssignedPlayerDiscordId, out var player);

        var memberExceptions = guildExceptions.Where(e => e.UserDiscordId == assignment.AssignedPlayerDiscordId).ToList();
        var memberPatterns = guildPatterns.Where(p => p.UserDiscordId == assignment.AssignedPlayerDiscordId).ToList();
        var resolved = availabilityResolutionService.ResolveForScope(eventLocalDate, eventLocalDate, memberExceptions, memberPatterns, guildId, guildBranchId);
        var availabilityStatus = resolved.Count > 0 ? resolved[0].Status : DayAvailabilityStatus.Available;

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
        };
    }
}
