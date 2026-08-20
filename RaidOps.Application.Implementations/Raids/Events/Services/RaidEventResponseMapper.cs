using RaidOps.Application.Contracts.Raids.Events.Responses;
using RaidOps.Application.Contracts.Raids.Zones.Responses;
using RaidOps.Application.Implementations.Raids.Helpers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids;
using RaidOps.Domain.Models.Reference;

namespace RaidOps.Application.Implementations.Raids.Events.Services;

/// <summary>
/// Maps a <see cref="RaidEvent"/> (with its target zones and slot assignments eagerly loaded) into
/// a <see cref="RaidEventResponse"/>, given the enrichment data (resolved availability, signups,
/// players, raid specs) already loaded for the requester's context. Shared by every query that
/// returns a full event — the board (many events at once) and a single event's own detail fetch.
/// </summary>
public static class RaidEventResponseMapper
{
    /// <inheritdoc cref="RaidEventResponseMapper"/>
    public static RaidEventResponse Map(RaidEvent raidEvent, RaidEventMappingContext ctx)
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

    private static RaidSlotAssignmentResponse MapAssignment(RaidSlotAssignment assignment, DateOnly eventLocalDate, Dictionary<string, RaidSignup> eventSignups, RaidEventMappingContext ctx)
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
