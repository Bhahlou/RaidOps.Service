using Microsoft.Extensions.DependencyInjection;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Characters.Services;
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
        return services;
    }
}
