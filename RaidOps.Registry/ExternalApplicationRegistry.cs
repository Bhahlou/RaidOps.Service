using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NetCord;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Hosting.Services.ComponentInteractions;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.ComponentInteractions;
using RaidOps.ExternalApplication.Contracts.Services.BNet;
using RaidOps.ExternalApplication.Contracts.Services.Discord;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.ExternalApplication.Implementations.BNet;
using RaidOps.ExternalApplication.Implementations.Bot.Services;
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

        // Plain HttpClientFactory (no typed client) so IEmojiService can fetch manifest image
        // bytes without tying its own lifetime to the transient default of a typed client.
        services.AddHttpClient();

        // Discord Gateway bot
        services
            .AddDiscordGateway(options =>
            {
                options.Token = configuration["Discord:BotToken"]!;
                options.Intents = GatewayIntents.Guilds | GatewayIntents.GuildUsers | GatewayIntents.GuildPresences;
            })
            .AddGatewayHandlers(typeof(DiscordBotService).Assembly)
            .AddApplicationCommands(options =>
            {
                // Command/parameter names and descriptions default to English in source (see
                // RaidCommandModule) — fr/de translations live here instead of as hardcoded
                // strings in .cs files, one JSON file per Discord locale, discovered by filename.
                options.LocalizationsProvider = new JsonLocalizationsProvider(new JsonLocalizationsProviderConfiguration
                {
                    DirectoryPath = Path.Combine(AppContext.BaseDirectory, "Bot", "Commands", "Localizations"),
                    FileNameFormat = "*.json",
                });
            })
            .AddComponentInteractions<ButtonInteraction, ButtonInteractionContext>()
            .AddComponentInteractions<StringMenuInteraction, StringMenuInteractionContext>();

        // Singleton: its application-emoji cache is synced once at bot startup (see ReadyHandler)
        // and must be shared across every later request-scoped IDiscordBotService/EmojiService use.
        services.AddSingleton<IEmojiService, EmojiService>();
        services.AddScoped<IDiscordBotService, DiscordBotService>();

        return services;
    }
}
