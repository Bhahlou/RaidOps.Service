using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RaidOps.ExternalApplication.Contracts.Services.Discord;
using RaidOps.ExternalApplication.Implementations.Services;

namespace RaidOps.Registry;

internal static class ExternalApplicationsRegistry
{
    internal static IServiceCollection SetupExternalApplicationsRegistry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpClient<IDiscordApiService, DiscordApiService>();
        return services;
    }
}
