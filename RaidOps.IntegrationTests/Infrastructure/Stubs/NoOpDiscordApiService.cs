using RaidOps.ExternalApplication.Contracts.Services.Discord;
using RaidOps.ExternalApplication.Contracts.Services.Discord.Responses;

namespace RaidOps.IntegrationTests.Infrastructure.Stubs;

/// <summary>
/// Stub implementation of <see cref="IDiscordApiService"/> for integration tests.
/// Returns predictable fake responses so tests that trigger Discord API calls
/// (e.g. the token-refresh flow) do not need a live Discord connection.
/// </summary>
internal class NoOpDiscordApiService : IDiscordApiService
{
    public Task<GetDiscordUserInfoResponse> GetCurrentUserAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new GetDiscordUserInfoResponse
        {
            Id = "stub-discord-id",
            Username = "StubUser",
            Avatar = null,
        });

    public Task<List<GetDiscordUserGuildResponse>> GetCurrentUserGuildsAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new List<GetDiscordUserGuildResponse>());

    public Task<RefreshDiscordTokenResponse> RefreshAccessTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new RefreshDiscordTokenResponse
        {
            AccessToken = "stub-new-access-token",
            RefreshToken = "stub-new-refresh-token",
            ExpiresIn = 604800,
        });
}
