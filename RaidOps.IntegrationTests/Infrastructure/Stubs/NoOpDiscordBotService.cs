using NetCord.Gateway;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

namespace RaidOps.IntegrationTests.Infrastructure.Stubs;

/// <summary>
/// No-op implementation of <see cref="IDiscordBotService"/> used in integration tests
/// to replace the real Discord Gateway bot, which requires a live connection.
/// </summary>
internal class NoOpDiscordBotService : IDiscordBotService
{
    public IGuildService Guilds { get; } = new NoOpGuildService();
    public IMessageService Messages => throw new NotSupportedException("Discord bot is not available in integration tests.");
}
