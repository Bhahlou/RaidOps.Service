using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RaidOps.API;
using RaidOps.ExternalApplication.Contracts.Services.BNet;
using RaidOps.ExternalApplication.Contracts.Services.Discord;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.IntegrationTests.Infrastructure.Stubs;
using System.Text;
using Testcontainers.PostgreSql;

namespace RaidOps.IntegrationTests.Infrastructure;

/// <summary>
/// Shared test server backed by a PostgreSQL Testcontainer.
/// One container instance is reused for all tests in a fixture class.
/// Migrations run automatically at first startup (replicated from Program.cs).
/// </summary>
public class RaidOpsWebApplicationFactory : WebApplicationFactory<ProgramEntryPoint>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("raidops_integration")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
                ["Jwt:Key"] = TestTokenBuilder.JwtKey,
                ["Jwt:Issuer"] = TestTokenBuilder.JwtIssuer,
                ["Jwt:Audience"] = TestTokenBuilder.JwtAudience,
                ["Jwt:AccessTokenExpirationMinutes"] = "60",
                ["Jwt:RefreshTokenExpirationDays"] = "30",
                ["Discord:ClientId"] = "test-client-id",
                ["Discord:ClientSecret"] = "test-client-secret",
                ["Discord:BotToken"] = "test-bot-token",
                ["Discord:BotPermissions"] = "0",
                ["FrontendUrl"] = "http://localhost:4200",
                ["BattleNet:ClientId"] = "test-bnet-client-id",
                ["BattleNet:ClientSecret"] = "test-bnet-secret",
                ["BattleNet:CallbackUrl"] = "http://localhost/bnet/callback",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            RemoveNetcordServices(services);
            ReplaceExternalApiServices(services);
            OverrideJwtOptions(services);
        });
    }

    // Program.cs captures jwtSettings BEFORE ConfigureAppConfiguration runs, so the JWT lambda
    // closes over an empty key and throws at first request. We remove that lambda entirely and
    // replace it with one that uses the test key.
    private static void OverrideJwtOptions(IServiceCollection services)
    {
        services.RemoveAll<IConfigureOptions<JwtBearerOptions>>();
        services.RemoveAll<IPostConfigureOptions<JwtBearerOptions>>();

        services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestTokenBuilder.JwtKey));
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = TestTokenBuilder.JwtIssuer,
                ValidAudience = TestTokenBuilder.JwtAudience,
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.FromSeconds(30),
            };
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = ctx =>
                {
                    ctx.Token = ctx.Request.Cookies["access_token"];
                    return Task.CompletedTask;
                },
            };
        });
    }

    private static void ReplaceExternalApiServices(IServiceCollection services)
    {
        services.RemoveAll<IDiscordApiService>();
        services.AddScoped<IDiscordApiService, NoOpDiscordApiService>();

        services.RemoveAll<IBnetApiService>();
        services.AddScoped<IBnetApiService, NoOpBnetApiService>();
    }

    private static void RemoveNetcordServices(IServiceCollection services)
    {
        // NetCord.Gateway requires a real Discord WebSocket connection — remove it and replace
        // IDiscordBotService with a configurable stub so tests can inject expected Discord data.
        var toRemove = services
            .Where(d =>
                (d.ImplementationType?.Assembly.GetName().Name?.StartsWith("NetCord") ?? false) ||
                (d.ServiceType.Assembly.GetName().Name?.StartsWith("NetCord") ?? false))
            .ToList();
        foreach (var descriptor in toRemove)
            services.Remove(descriptor);

        var botDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IDiscordBotService));
        if (botDescriptor is not null)
            services.Remove(botDescriptor);

        services.AddScoped<IDiscordBotService, NoOpDiscordBotService>();
    }

    Task IAsyncLifetime.InitializeAsync() => _postgres.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }
}
