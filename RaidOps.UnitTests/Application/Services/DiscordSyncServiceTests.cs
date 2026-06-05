using FluentAssertions;
using Moq;
using RaidOps.Application.Implementations.Services;
using RaidOps.Domain.Models.Discord;
using RaidOps.ExternalApplication.Contracts.Services.Discord;
using RaidOps.ExternalApplication.Contracts.Services.Discord.Responses;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Services;

public class DiscordSyncServiceTests
{
    private readonly Mock<IDiscordApiService>    _discord     = new();
    private readonly Mock<IUsersRepository>      _users       = new();
    private readonly Mock<IGuildsRepository>     _guilds      = new();
    private readonly Mock<IUserGuildsRepository> _userGuilds  = new();
    private readonly DiscordSyncService          _sut;

    private const string DiscordId    = "123456789";
    private const string AccessToken  = "access-token";
    private const string RefreshToken = "refresh-token";

    public DiscordSyncServiceTests()
    {
        _sut = new DiscordSyncService(_discord.Object, _users.Object, _guilds.Object, _userGuilds.Object);

        // Default happy-path Discord API responses
        _discord.Setup(d => d.GetCurrentUserAsync(AccessToken, default))
            .ReturnsAsync(new GetDiscordUserInfoResponse { Id = DiscordId, Username = "Bhahlou", Avatar = "hash123" });

        _discord.Setup(d => d.GetCurrentUserGuildsAsync(AccessToken, default))
            .ReturnsAsync([new GetDiscordUserGuildResponse { Id = "guild-1", Name = "My Guild" }]);

        _guilds.Setup(g => g.UpsertRangeAsync(It.IsAny<List<Guild>>(), default)).Returns(Task.CompletedTask);
        _userGuilds.Setup(g => g.ReplaceUserGuildsAsync(It.IsAny<string>(), It.IsAny<List<UserGuild>>(), default)).Returns(Task.CompletedTask);
    }

    // ── Signup flow (accessToken + refreshToken provided) ─────────────────────

    [Fact]
    public async Task SignupSync_NewUser_CreatesUserWithCorrectFields()
    {
        _users.Setup(u => u.GetByDiscordIdAsync(DiscordId, default)).ReturnsAsync((User?)null);
        _users.Setup(u => u.AddAsync(It.IsAny<User>(), default))
            .ReturnsAsync((User u, CancellationToken _) => u);

        var result = await _sut.SyncUserAndGuildsAsync(DiscordId, AccessToken, RefreshToken);

        result.DiscordId.Should().Be(DiscordId);
        result.Name.Should().Be("Bhahlou");
        result.AvatarHash.Should().Be("hash123");
        result.RefreshToken.Should().Be(RefreshToken);
    }

    [Fact]
    public async Task SignupSync_ExistingUser_UpdatesFields()
    {
        var existing = new User { DiscordId = DiscordId, Name = "OldName", RefreshToken = "old-token" };
        _users.Setup(u => u.GetByDiscordIdAsync(DiscordId, default)).ReturnsAsync(existing);
        _users.Setup(u => u.UpdateAsync(It.IsAny<User>(), default))
            .ReturnsAsync((User u, CancellationToken _) => u);

        var result = await _sut.SyncUserAndGuildsAsync(DiscordId, AccessToken, RefreshToken);

        result.Name.Should().Be("Bhahlou");
        result.AvatarHash.Should().Be("hash123");
        result.RefreshToken.Should().Be(RefreshToken);
        _users.Verify(u => u.AddAsync(It.IsAny<User>(), default), Times.Never);
    }

    [Fact]
    public async Task SignupSync_UpsertsGuildsAndReplacesUserGuilds()
    {
        _users.Setup(u => u.GetByDiscordIdAsync(DiscordId, default)).ReturnsAsync((User?)null);
        _users.Setup(u => u.AddAsync(It.IsAny<User>(), default))
            .ReturnsAsync((User u, CancellationToken _) => u);

        await _sut.SyncUserAndGuildsAsync(DiscordId, AccessToken, RefreshToken);

        _guilds.Verify(g => g.UpsertRangeAsync(
            It.Is<List<Guild>>(list => list.Count == 1 && list[0].Id == "guild-1"), default), Times.Once);

        _userGuilds.Verify(g => g.ReplaceUserGuildsAsync(
            DiscordId,
            It.Is<List<UserGuild>>(list => list.Count == 1 && list[0].GuildId == "guild-1"),
            default), Times.Once);
    }

    // ── Refresh token flow (only discordId) ───────────────────────────────────

    [Fact]
    public async Task RefreshSync_UserNotFound_ThrowsInvalidOperationException()
    {
        _users.Setup(u => u.GetByDiscordIdAsync(DiscordId, default)).ReturnsAsync((User?)null);

        var act = () => _sut.SyncUserAndGuildsAsync(DiscordId);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task RefreshSync_UsesRefreshedAccessToken_NotStoredOne()
    {
        const string storedRefreshToken  = "stored-refresh";
        const string newAccessToken      = "new-access-token";
        const string newRefreshToken     = "new-refresh-token";

        var existing = new User { DiscordId = DiscordId, Name = "Bhahlou", RefreshToken = storedRefreshToken };
        _users.Setup(u => u.GetByDiscordIdAsync(DiscordId, default)).ReturnsAsync(existing);
        _users.Setup(u => u.UpdateAsync(It.IsAny<User>(), default))
            .ReturnsAsync((User u, CancellationToken _) => u);

        _discord.Setup(d => d.RefreshAccessTokenAsync(storedRefreshToken, default))
            .ReturnsAsync(new RefreshDiscordTokenResponse { AccessToken = newAccessToken, RefreshToken = newRefreshToken });

        _discord.Setup(d => d.GetCurrentUserAsync(newAccessToken, default))
            .ReturnsAsync(new GetDiscordUserInfoResponse { Id = DiscordId, Username = "Bhahlou", Avatar = null });

        _discord.Setup(d => d.GetCurrentUserGuildsAsync(newAccessToken, default))
            .ReturnsAsync([]);

        var result = await _sut.SyncUserAndGuildsAsync(DiscordId);

        result.RefreshToken.Should().Be(newRefreshToken);
        _discord.Verify(d => d.GetCurrentUserAsync(newAccessToken, default), Times.Once);
        _discord.Verify(d => d.GetCurrentUserAsync(storedRefreshToken, default), Times.Never);
    }

    [Fact]
    public async Task RefreshSync_PersistsNewRefreshToken()
    {
        const string newRefreshToken = "new-refresh-token";

        var existing = new User { DiscordId = DiscordId, Name = "Bhahlou", RefreshToken = "old-refresh" };
        _users.Setup(u => u.GetByDiscordIdAsync(DiscordId, default)).ReturnsAsync(existing);
        _users.Setup(u => u.UpdateAsync(It.IsAny<User>(), default))
            .ReturnsAsync((User u, CancellationToken _) => u);

        _discord.Setup(d => d.RefreshAccessTokenAsync("old-refresh", default))
            .ReturnsAsync(new RefreshDiscordTokenResponse { AccessToken = "new-access", RefreshToken = newRefreshToken });

        _discord.Setup(d => d.GetCurrentUserAsync("new-access", default))
            .ReturnsAsync(new GetDiscordUserInfoResponse { Id = DiscordId, Username = "Bhahlou" });

        _discord.Setup(d => d.GetCurrentUserGuildsAsync("new-access", default))
            .ReturnsAsync([]);

        var result = await _sut.SyncUserAndGuildsAsync(DiscordId);

        result.RefreshToken.Should().Be(newRefreshToken);
        _users.Verify(u => u.UpdateAsync(It.Is<User>(u => u.RefreshToken == newRefreshToken), default), Times.Once);
    }
}
