using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using RaidOps.ExternalApplication.Contracts.Services.BNet;
using RaidOps.ExternalApplication.Contracts.Services.Discord;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.ExternalApplication.Implementations.BNet;
using RaidOps.ExternalApplication.Implementations.Bot;
using RaidOps.ExternalApplication.Implementations.Services;

namespace RaidOps.Registry;

internal static class ExternalApplicationsRegistry
{
    internal static IServiceCollection SetupExternalApplicationsRegistry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Discord REST API (OAuth2 user flow)
        services.AddHttpClient<IDiscordApiService, DiscordApiService>();

        // Discord deploy notification (webhook, posted once on startup)
        services.AddHttpClient<IDiscordDeployNotifier, DiscordDeployNotifier>();

        // Battle.net API (OAuth2 + character data)
        services.AddHttpClient<IBnetApiService, BnetApiService>();


        // Discord Gateway bot
        services
            .AddDiscordGateway(options =>
            {
                options.Token = configuration["Discord:BotToken"]!;
                options.Intents = GatewayIntents.Guilds | GatewayIntents.GuildUsers | GatewayIntents.GuildPresences;
            })
            .AddGatewayHandlers(typeof(DiscordBotService).Assembly);

        services.AddScoped<IDiscordBotService, DiscordBotService>();

        return services;
    }
}
