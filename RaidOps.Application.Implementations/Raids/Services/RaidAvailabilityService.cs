using RaidOps.Application.Contracts.Calendar.Availability.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Helpers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Calendar;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Services;

/// <inheritdoc cref="IRaidAvailabilityService"/>
public class RaidAvailabilityService(
    IGuildsRepository guildsRepository,
    IAvailabilityRepository availabilityRepository,
    IAvailabilityResolutionService availabilityResolutionService) : IRaidAvailabilityService
{
    /// <inheritdoc/>
    public async Task<bool> IsPlayerUnavailableAsync(string playerDiscordId, string guildId, int guildBranchId, DateTime eventStartsAtUtc, CancellationToken cancellationToken = default)
    {
        var guild = await guildsRepository.GetByIdAsync(guildId, cancellationToken);
        var eventLocalDateTime = GuildTimeHelper.ToGuildLocalDateTime(eventStartsAtUtc, guild?.Timezone);
        var eventLocalDate = DateOnly.FromDateTime(eventLocalDateTime);
        var eventLocalTime = TimeOnly.FromDateTime(eventLocalDateTime);

        var lookup = await LoadRosterAvailabilityAsync([playerDiscordId], guildId, guildBranchId, eventLocalDate, eventLocalDate, cancellationToken);
        return lookup.IsUnavailableAt(playerDiscordId, eventLocalDate, eventLocalTime);
    }

    /// <inheritdoc/>
    public async Task<IRaidAvailabilityLookup> LoadRosterAvailabilityAsync(
        IEnumerable<string> playerDiscordIds, string guildId, int guildBranchId, DateOnly rangeStart, DateOnly rangeEnd, CancellationToken cancellationToken = default)
    {
        var ids = playerDiscordIds as ICollection<string> ?? [.. playerDiscordIds];
        var exceptions = await availabilityRepository.GetExceptionsOverlappingForUsersAsync(ids, rangeStart, rangeEnd, cancellationToken);
        var patterns = await availabilityRepository.GetPatternsForUsersAsync(ids, cancellationToken);

        return new RaidAvailabilityLookup(exceptions, patterns, availabilityResolutionService, guildId, guildBranchId);
    }

    private sealed class RaidAvailabilityLookup(
        List<AvailabilityDeclaration> exceptions,
        List<RecurringAvailabilityPattern> patterns,
        IAvailabilityResolutionService resolutionService,
        string guildId,
        int guildBranchId) : IRaidAvailabilityLookup
    {
        public DayAvailabilityStatus ResolveStatus(string playerDiscordId, DateOnly localDate)
        {
            var resolved = Resolve(playerDiscordId, localDate);
            return resolved.Count > 0 ? resolved[0].Status : DayAvailabilityStatus.Available;
        }

        public bool IsUnavailableAt(string playerDiscordId, DateOnly localDate, TimeOnly localTime)
        {
            var resolved = Resolve(playerDiscordId, localDate);
            if (resolved.Count == 0)
                return false;

            var day = resolved[0];
            if (day.Status == DayAvailabilityStatus.Absent)
                return true;

            if (day.Status == DayAvailabilityStatus.Partial)
            {
                var withinWindow =
                    (day.AvailableFrom == null || localTime >= day.AvailableFrom.Value) &&
                    (day.AvailableUntil == null || localTime <= day.AvailableUntil.Value);
                return !withinWindow;
            }

            return false;
        }

        private List<ResolvedDayAvailabilityResponse> Resolve(string playerDiscordId, DateOnly localDate)
        {
            var memberExceptions = exceptions.Where(e => e.UserDiscordId == playerDiscordId).ToList();
            var memberPatterns = patterns.Where(p => p.UserDiscordId == playerDiscordId).ToList();
            return resolutionService.ResolveForScope(localDate, localDate, memberExceptions, memberPatterns, guildId, guildBranchId);
        }
    }
}
