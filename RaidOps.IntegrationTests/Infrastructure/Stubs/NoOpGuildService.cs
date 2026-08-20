using NetCord;
using NetCord.Gateway;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

namespace RaidOps.IntegrationTests.Infrastructure.Stubs;

/// <summary>
/// No-op implementation of <see cref="IGuildService"/> that simulates a bot
/// present in every guild by returning without throwing.
/// </summary>
internal class NoOpGuildService : IGuildService
{
    public Guild Get(string guildId, CancellationToken cancellationToken = default) => null!;

    public IEnumerable<GuildUser> GetUsers(string guildId, CancellationToken cancellationToken = default) => [];

    public IEnumerable<GuildUser> GetAdmins(string guildId, CancellationToken cancellationToken = default) => [];

    public IEnumerable<Role> GetRoles(string guildId, CancellationToken cancellationToken = default) => [];

    public IEnumerable<DiscordChannelInfo> GetChannels(string guildId, CancellationToken cancellationToken = default) => [];

    public DiscordCategoriesInfo GetCategories(string guildId, CancellationToken cancellationToken = default) => new(true, []);

    public Task<DiscordChannelInfo> CreateTextChannelAsync(string guildId, string name, string? categoryId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new DiscordChannelInfo(0, name, []));

    public Task DeleteChannelAsync(string channelId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public string? GetPreferredLocale(string guildId, CancellationToken cancellationToken = default) => null;

    public GuildUser? GetUser(string guildId, string userId, CancellationToken cancellationToken = default) => null;
}
