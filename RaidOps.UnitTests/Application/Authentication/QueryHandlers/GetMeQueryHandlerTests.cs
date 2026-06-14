using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Authentication.Queries;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Implementations.Authentication.QueryHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Authentication.QueryHandlers;

public class GetMeQueryHandlerTests
{
    private readonly Mock<IUsersRepository> _users = new();
    private readonly GetMeQueryHandler      _sut;

    private const string DiscordId = "user-1";

    private static readonly GetMeQuery Query = new() { DiscordId = DiscordId };

    public GetMeQueryHandlerTests()
    {
        _sut = new GetMeQueryHandler(_users.Object);
    }

    // ── Guard clause ─────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_UserNotFound_ReturnsUserNotFound()
    {
        _users.Setup(r => r.GetByDiscordIdWithGuildsAsync(DiscordId, default))
            .ReturnsAsync((User?)null);

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.UserNotFound);
    }

    // ── Field mapping ─────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_UserFound_MapsProfileFields()
    {
        _users.Setup(r => r.GetByDiscordIdWithGuildsAsync(DiscordId, default))
            .ReturnsAsync(new User
            {
                DiscordId   = DiscordId,
                Name        = "Bhahlou",
                AvatarHash  = "abc123",
                RefreshToken = "tok",
                UserGuilds  = [],
            });

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.DiscordId.Should().Be(DiscordId);
        result.Value.Name.Should().Be("Bhahlou");
        result.Value.AvatarHash.Should().Be("abc123");
    }

    // ── Guild filtering ───────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_AdminOnUnregisteredGuild_GuildIncluded()
    {
        _users.Setup(r => r.GetByDiscordIdWithGuildsAsync(DiscordId, default))
            .ReturnsAsync(MakeUser([MakeUserGuild("g1", isAdmin: true, isRegistered: false)]));

        var result = await _sut.HandleAsync(Query, default);

        result.Value!.Guilds.Should().ContainSingle(g => g.Id == "g1");
    }

    [Fact]
    public async Task HandleAsync_NotAdminOnRegisteredGuild_GuildIncluded()
    {
        _users.Setup(r => r.GetByDiscordIdWithGuildsAsync(DiscordId, default))
            .ReturnsAsync(MakeUser([MakeUserGuild("g1", isAdmin: false, isRegistered: true)]));

        var result = await _sut.HandleAsync(Query, default);

        result.Value!.Guilds.Should().ContainSingle(g => g.Id == "g1");
    }

    [Fact]
    public async Task HandleAsync_NeitherAdminNorRegistered_GuildExcluded()
    {
        _users.Setup(r => r.GetByDiscordIdWithGuildsAsync(DiscordId, default))
            .ReturnsAsync(MakeUser([MakeUserGuild("g1", isAdmin: false, isRegistered: false)]));

        var result = await _sut.HandleAsync(Query, default);

        result.Value!.Guilds.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_MixedGuilds_OnlyEligibleOnesReturned()
    {
        _users.Setup(r => r.GetByDiscordIdWithGuildsAsync(DiscordId, default))
            .ReturnsAsync(MakeUser(
            [
                MakeUserGuild("g-admin",      isAdmin: true,  isRegistered: false),
                MakeUserGuild("g-registered", isAdmin: false, isRegistered: true),
                MakeUserGuild("g-both",       isAdmin: true,  isRegistered: true),
                MakeUserGuild("g-none",       isAdmin: false, isRegistered: false),
            ]));

        var result = await _sut.HandleAsync(Query, default);

        result.Value!.Guilds.Should().HaveCount(3);
        result.Value.Guilds.Should().NotContain(g => g.Id == "g-none");
    }

    [Fact]
    public async Task HandleAsync_GuildResponse_MapsAllFields()
    {
        _users.Setup(r => r.GetByDiscordIdWithGuildsAsync(DiscordId, default))
            .ReturnsAsync(MakeUser([MakeUserGuild("g1", isAdmin: true, isRegistered: true,
                name: "RaidOps", iconHash: "icon42", timezone: "Europe/Paris", rosterMode: RosterMode.Open)]));

        var result = await _sut.HandleAsync(Query, default);

        var guild = result.Value!.Guilds.Single();
        guild.Id.Should().Be("g1");
        guild.Name.Should().Be("RaidOps");
        guild.IconHash.Should().Be("icon42");
        guild.IsRegistered.Should().BeTrue();
        guild.IsAdmin.Should().BeTrue();
        guild.IsConfigured.Should().BeTrue();
    }

    // ── IsConfigured mapping ──────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_BothTimezoneAndRosterModeSet_IsConfiguredTrue()
    {
        _users.Setup(r => r.GetByDiscordIdWithGuildsAsync(DiscordId, default))
            .ReturnsAsync(MakeUser([MakeUserGuild("g1", isAdmin: true, isRegistered: true,
                timezone: "Europe/Paris", rosterMode: RosterMode.Open)]));

        var result = await _sut.HandleAsync(Query, default);

        result.Value!.Guilds.Single().IsConfigured.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_TimezoneNull_IsConfiguredFalse()
    {
        _users.Setup(r => r.GetByDiscordIdWithGuildsAsync(DiscordId, default))
            .ReturnsAsync(MakeUser([MakeUserGuild("g1", isAdmin: true, isRegistered: true,
                timezone: null, rosterMode: RosterMode.Open)]));

        var result = await _sut.HandleAsync(Query, default);

        result.Value!.Guilds.Single().IsConfigured.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_RosterModeNull_IsConfiguredFalse()
    {
        _users.Setup(r => r.GetByDiscordIdWithGuildsAsync(DiscordId, default))
            .ReturnsAsync(MakeUser([MakeUserGuild("g1", isAdmin: true, isRegistered: true,
                timezone: "Europe/Paris", rosterMode: null)]));

        var result = await _sut.HandleAsync(Query, default);

        result.Value!.Guilds.Single().IsConfigured.Should().BeFalse();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static User MakeUser(ICollection<UserGuild> guilds) => new()
    {
        DiscordId    = DiscordId,
        Name         = "Bhahlou",
        RefreshToken = "tok",
        UserGuilds   = guilds,
    };

    private static UserGuild MakeUserGuild(
        string       guildId,
        bool         isAdmin,
        bool         isRegistered,
        string       name       = "Guild Name",
        string?      iconHash   = null,
        string?      timezone   = null,
        RosterMode?  rosterMode = null) => new()
    {
        UserDiscordId = DiscordId,
        GuildId       = guildId,
        IsAdmin       = isAdmin,
        Guild = new Guild
        {
            Id           = guildId,
            Name         = name,
            IconHash     = iconHash,
            IsRegistered = isRegistered,
            Timezone     = timezone,
            RosterMode   = rosterMode,
        },
    };
}
