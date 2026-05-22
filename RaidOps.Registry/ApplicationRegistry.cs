using Microsoft.Extensions.DependencyInjection;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Services;

namespace RaidOps.Registry;

internal static class ApplicationRegistry
{
    internal static IServiceCollection SetupApplicationRegistry(this IServiceCollection services)
    {
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IDiscordSyncService, DiscordSyncService>();
        services.AddScoped<IRaidOpsAuthService, RaidOpsAuthService>();
        return services;
    }
}
