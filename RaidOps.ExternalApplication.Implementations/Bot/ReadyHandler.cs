using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Specs.Queries;
using RaidOps.Application.Contracts.Specs.Responses;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

namespace RaidOps.ExternalApplication.Implementations.Bot;

/// <summary>
/// Gateway event handler for the Discord <c>READY</c> event — fires once per connection, after the
/// bot's own application/user info is available. Used to sync <see cref="IEmojiService"/>'s
/// application emoji set (class icons, spec icons, ...) so every environment self-heals its emoji
/// set on every boot, with no manual per-environment step.
/// </summary>
/// <remarks>
/// NetCord registers gateway handlers as singletons. <see cref="IQueryDispatcher"/> is scoped, so a
/// fresh <see cref="IServiceScope"/> is created here to avoid consuming a scoped service from a
/// singleton — same pattern as <see cref="GuildDeleteHandler"/>.
/// </remarks>
public class ReadyHandler(
    IEmojiService emojiService,
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<ReadyHandler> logger) : IReadyGatewayHandler
{
    /// <inheritdoc/>
    public async ValueTask HandleAsync(ReadyEventArgs arg)
    {
        try
        {
            var blizzardClassIconBaseUrl = configuration["Discord:BlizzardClassIconBaseUrl"]!;
            var entries = ApplicationEmojiManifest.ClassIcons(blizzardClassIconBaseUrl).ToList();

            await using (var scope = scopeFactory.CreateAsyncScope())
            {
                var queryDispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();
                var specs = await queryDispatcher.DispatchAsync<GetSpecsQuery, IEnumerable<SpecDto>>(new GetSpecsQuery());

                if (specs.IsSuccess)
                    entries.AddRange(ApplicationEmojiManifest.SpecIcons(specs.Value!));
                else
                    logger.LogWarning("Failed to fetch specs for emoji sync: {Error}", specs.Error);
            }

            await emojiService.SyncAsync(entries);
        }
        catch (Exception ex)
        {
            // Never let a sync failure take the gateway connection down — worst case, some
            // notifications render without their icon until the next successful boot.
            logger.LogError(ex, "Failed to sync application emojis on startup.");
        }
    }
}
