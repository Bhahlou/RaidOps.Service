using Microsoft.Extensions.DependencyInjection;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Calendar.Availability.Services;
using RaidOps.Application.Implementations.Characters.Services;
using RaidOps.Application.Implementations.Guilds.Access;
using RaidOps.Application.Implementations.Guilds.Notifications;
using RaidOps.Application.Implementations.Notifications.Services;
using RaidOps.Application.Implementations.Raids.Services;
using RaidOps.Application.Implementations.Services;

namespace RaidOps.Registry;

internal static class ApplicationRegistry
{
    internal static IServiceCollection SetupApplicationRegistry(this IServiceCollection services)
    {
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IDiscordSyncService, DiscordSyncService>();
        services.AddScoped<IRaidOpsAuthService, RaidOpsAuthService>();
        services.AddScoped<ISpecResolverService, SpecResolverService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IGuildAccessService, GuildAccessService>();
        services.AddScoped<IGuildJoinEligibilityService, GuildJoinEligibilityService>();
        services.AddScoped<IUserNotificationService, UserNotificationService>();
        services.AddScoped<IAvailabilityResolutionService, AvailabilityResolutionService>();
        services.AddScoped<IActiveRosterBranchResolver, ActiveRosterBranchResolver>();
        services.AddScoped<IGuildNotificationDispatcher, GuildNotificationDispatcher>();
        services.AddScoped<IAbsenceNotificationContentBuilder, AbsenceNotificationContentBuilder>();
        services.AddScoped<IAvailabilityChangeAnnouncer, AvailabilityChangeAnnouncer>();
        services.AddScoped<IRaidLockoutService, RaidLockoutService>();
        services.AddScoped<IRaidAvailabilityService, RaidAvailabilityService>();
        services.AddScoped<IRaidLockoutConflictChecker, RaidLockoutConflictChecker>();
        services.AddScoped<IRaidGridAndZoneValidator, RaidGridAndZoneValidator>();
        return services;
    }
}
