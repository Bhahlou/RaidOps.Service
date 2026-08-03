using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

namespace RaidOps.IntegrationTests.Infrastructure.Stubs;

/// <summary>
/// No-op implementation of <see cref="IEmojiService"/> that simulates no application emoji
/// ever having synced — <see cref="GetMarkdown"/> returning <c>null</c> is the documented,
/// non-exceptional behavior for that case, so callers fall back to plain text as they would
/// against a real bot that hasn't synced yet.
/// </summary>
internal class NoOpEmojiService : IEmojiService
{
    public Task SyncAsync(IEnumerable<(string Name, string SourceUrl)> entries, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public string? GetMarkdown(string name) => null;
}
