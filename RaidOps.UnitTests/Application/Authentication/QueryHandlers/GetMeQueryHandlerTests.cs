using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Authentication.Queries;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Notifications.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Authentication.QueryHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Authentication.QueryHandlers;

public class GetMeQueryHandlerTests
{
    private readonly Mock<IUsersRepository>              _users               = new();
    private readonly Mock<IGuildAccessService>           _access              = new();
    private readonly Mock<IUserNotificationService>      _userNotifications   = new();
    private readonly Mock<IActiveRosterBranchResolver>   _activeRosterBranches = new();
    private readonly Mock<ISeenChangelogEntryRepository> _seenChangelog       = new();
    private readonly GetMeQueryHandler                   _sut;

    private const string DiscordId = "user-1";

    private static readonly GetMeQuery Query = new() { DiscordId = DiscordId };

    public GetMeQueryHandlerTests()
    {
        // No active notifications by default — individual tests opt in.
        // Notification derivation rules themselves are owned by IUserNotificationService's
        // registered INotificationSignalProvider implementations (e.g. RoleMappingNotificationProvider),
        // not by this handler — GetMeQueryHandler only needs to prove it forwards the result.
        _userNotifications.Setup(n => n.GetActiveNotificationsAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<UserGuild>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        // No active roster branches by default — individual tests opt in.
        _activeRosterBranches.Setup(r => r.GetActiveBranchesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        // No seen changelog entries by default — individual tests opt in.
        _seenChangelog.Setup(r => r.GetSeenEntryIdsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _sut = new GetMeQueryHandler(_users.Object, _access.Object, _userNotifications.Object, _activeRosterBranches.Object, _seenChangelog.Object);
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

    [Fact]
    public async Task HandleAsync_UserHasSeenChangelogEntries_MapsThemIntoResponse()
    {
        _users.Setup(r => r.GetByDiscordIdWithGuildsAsync(DiscordId, default))
            .ReturnsAsync(MakeUser([]));
        _seenChangelog.Setup(r => r.GetSeenEntryIdsAsync(DiscordId, default))
            .ReturnsAsync(["2026-08-02-raid-notifications"]);

        var result = await _sut.HandleAsync(Query, default);

        result.Value!.SeenChangelogEntryIds.Should().ContainSingle().Which.Should().Be("2026-08-02-raid-notifications");
    }

    [Fact]
    public async Task HandleAsync_UserHasNoSeenChangelogEntries_ReturnsEmptyList()
    {
        _users.Setup(r => r.GetByDiscordIdWithGuildsAsync(DiscordId, default))
            .ReturnsAsync(MakeUser([]));

        var result = await _sut.HandleAsync(Query, default);

        result.Value!.SeenChangelogEntryIds.Should().BeEmpty();
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
                name: "RaidOps", iconHash: "icon42", timezone: "Europe/Paris", language: "en")]));

        var result = await _sut.HandleAsync(Query, default);

        var guild = result.Value!.Guilds.Single();
        guild.Id.Should().Be("g1");
        guild.Name.Should().Be("RaidOps");
        guild.IconHash.Should().Be("icon42");
        guild.IsRegistered.Should().BeTrue();
        guild.IsAdmin.Should().BeTrue();
        guild.IsConfigured.Should().BeTrue();
    }

    // ── AccessLevel mapping ───────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_AdminMembership_AccessLevelIsOfficerWithoutComputingBranches()
    {
        var userGuild = MakeUserGuild("g1", isAdmin: true, isRegistered: true);
        _users.Setup(r => r.GetByDiscordIdWithGuildsAsync(DiscordId, default))
            .ReturnsAsync(MakeUser([userGuild]));

        var result = await _sut.HandleAsync(Query, default);

        result.Value!.Guilds.Single().AccessLevel.Should().Be(GuildAccessLevel.Officer);
    }

    [Fact]
    public async Task HandleAsync_NonAdmin_AccessLevelIsMaxAcrossBranches()
    {
        var branch = MakeBranch(branchId: 1);
        var userGuild = MakeUserGuild("g1", isAdmin: false, isRegistered: true, branches: [branch]);
        _users.Setup(r => r.GetByDiscordIdWithGuildsAsync(DiscordId, default))
            .ReturnsAsync(MakeUser([userGuild]));
        _access.Setup(a => a.ComputeAccessLevel(userGuild, branch, default)).Returns(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(Query, default);

        result.Value!.Guilds.Single().AccessLevel.Should().Be(GuildAccessLevel.Roster);
    }

    [Fact]
    public async Task HandleAsync_NonAdmin_NoActiveBranches_AccessLevelIsPublic()
    {
        var userGuild = MakeUserGuild("g1", isAdmin: false, isRegistered: true, branches: []);
        _users.Setup(r => r.GetByDiscordIdWithGuildsAsync(DiscordId, default))
            .ReturnsAsync(MakeUser([userGuild]));

        var result = await _sut.HandleAsync(Query, default);

        result.Value!.Guilds.Single().AccessLevel.Should().Be(GuildAccessLevel.Public);
    }

    // ── Branches mapping ───────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_ActiveBranch_MappedIntoResponse()
    {
        var branch = MakeBranch(branchId: 2, isActive: true);
        var userGuild = MakeUserGuild("g1", isAdmin: false, isRegistered: true, branches: [branch]);
        _users.Setup(r => r.GetByDiscordIdWithGuildsAsync(DiscordId, default))
            .ReturnsAsync(MakeUser([userGuild]));
        _access.Setup(a => a.ComputeAccessLevel(userGuild, branch, default)).Returns(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(Query, default);

        var mapped = result.Value!.Guilds.Single().Branches.Single();
        mapped.Id.Should().Be(branch.Id);
        mapped.BranchId.Should().Be(2);
        mapped.BranchName.Should().Be("Classic Era");
        mapped.AccessLevel.Should().Be(GuildAccessLevel.Roster);
    }

    [Fact]
    public async Task HandleAsync_DeactivatedBranch_ExcludedFromResponse()
    {
        var branch = MakeBranch(branchId: 3, isActive: false);
        var userGuild = MakeUserGuild("g1", isAdmin: false, isRegistered: true, branches: [branch]);
        _users.Setup(r => r.GetByDiscordIdWithGuildsAsync(DiscordId, default))
            .ReturnsAsync(MakeUser([userGuild]));

        var result = await _sut.HandleAsync(Query, default);

        result.Value!.Guilds.Single().Branches.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_BranchInActiveRosterBranches_HasActiveCharacterTrue()
    {
        var branch = MakeBranch(branchId: 2, isActive: true);
        var userGuild = MakeUserGuild("g1", isAdmin: false, isRegistered: true, branches: [branch]);
        _users.Setup(r => r.GetByDiscordIdWithGuildsAsync(DiscordId, default))
            .ReturnsAsync(MakeUser([userGuild]));
        _access.Setup(a => a.ComputeAccessLevel(userGuild, branch, default)).Returns(GuildAccessLevel.Roster);
        _activeRosterBranches.Setup(r => r.GetActiveBranchesAsync(DiscordId, default))
            .ReturnsAsync([new ActiveRosterBranch("g1", branch.Id)]);

        var result = await _sut.HandleAsync(Query, default);

        result.Value!.Guilds.Single().Branches.Single().HasActiveCharacter.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_BranchNotInActiveRosterBranches_HasActiveCharacterFalse()
    {
        var branch = MakeBranch(branchId: 2, isActive: true);
        var userGuild = MakeUserGuild("g1", isAdmin: false, isRegistered: true, branches: [branch]);
        _users.Setup(r => r.GetByDiscordIdWithGuildsAsync(DiscordId, default))
            .ReturnsAsync(MakeUser([userGuild]));
        _access.Setup(a => a.ComputeAccessLevel(userGuild, branch, default)).Returns(GuildAccessLevel.Roster);
        _activeRosterBranches.Setup(r => r.GetActiveBranchesAsync(DiscordId, default))
            .ReturnsAsync([new ActiveRosterBranch("other-guild", 999)]);

        var result = await _sut.HandleAsync(Query, default);

        result.Value!.Guilds.Single().Branches.Single().HasActiveCharacter.Should().BeFalse();
    }

    // ── IsConfigured mapping ──────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_BothTimezoneAndLanguageSet_IsConfiguredTrue()
    {
        _users.Setup(r => r.GetByDiscordIdWithGuildsAsync(DiscordId, default))
            .ReturnsAsync(MakeUser([MakeUserGuild("g1", isAdmin: true, isRegistered: true,
                timezone: "Europe/Paris", language: "en")]));

        var result = await _sut.HandleAsync(Query, default);

        result.Value!.Guilds.Single().IsConfigured.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_TimezoneNull_IsConfiguredFalse()
    {
        _users.Setup(r => r.GetByDiscordIdWithGuildsAsync(DiscordId, default))
            .ReturnsAsync(MakeUser([MakeUserGuild("g1", isAdmin: true, isRegistered: true,
                timezone: null, language: "en")]));

        var result = await _sut.HandleAsync(Query, default);

        result.Value!.Guilds.Single().IsConfigured.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_LanguageNull_IsConfiguredFalse()
    {
        _users.Setup(r => r.GetByDiscordIdWithGuildsAsync(DiscordId, default))
            .ReturnsAsync(MakeUser([MakeUserGuild("g1", isAdmin: true, isRegistered: true,
                timezone: "Europe/Paris", language: null)]));

        var result = await _sut.HandleAsync(Query, default);

        result.Value!.Guilds.Single().IsConfigured.Should().BeFalse();
    }

    // ── Notifications ──────────────────────────────────────────────────────
    // Derivation rules (role mapping, onboarding exclusion, dismissal, admin-only) now live in
    // IUserNotificationService/INotificationSignalProvider implementations — this handler only
    // needs to prove it forwards the eligible guilds in and the result out untouched.

    [Fact]
    public async Task HandleAsync_ForwardsEligibleGuildsToUserNotificationServiceAndReturnsItsResult()
    {
        var userGuild = MakeUserGuild("g1", isAdmin: true, isRegistered: true, name: "RaidOps");
        _users.Setup(r => r.GetByDiscordIdWithGuildsAsync(DiscordId, default))
            .ReturnsAsync(MakeUser([userGuild]));
        var expected = new List<NotificationResponse>
        {
            new() { Type = NotificationType.BranchOfficerRolesNotConfigured, GuildId = "g1", GuildName = "RaidOps" },
        };
        _userNotifications.Setup(n => n.GetActiveNotificationsAsync(
                DiscordId, It.Is<IReadOnlyList<UserGuild>>(g => g.Count == 1 && g[0] == userGuild), default))
            .ReturnsAsync(expected);

        var result = await _sut.HandleAsync(Query, default);

        result.Value!.Notifications.Should().BeEquivalentTo(expected);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static User MakeUser(ICollection<UserGuild> guilds) => new()
    {
        DiscordId    = DiscordId,
        Name         = "Bhahlou",
        RefreshToken = "tok",
        UserGuilds   = guilds,
    };

    private static GuildBranch MakeBranch(int branchId, bool isActive = true) => new()
    {
        Id = branchId, GuildId = "g1", BranchId = branchId, IsActive = isActive,
        Branch = new Branch { Id = branchId, Name = "Classic Era" },
    };

    private static UserGuild MakeUserGuild(
        string       guildId,
        bool         isAdmin,
        bool         isRegistered,
        string       name       = "Guild Name",
        string?      iconHash   = null,
        string?      timezone   = null,
        string?      language   = null,
        List<GuildBranch>? branches = null) => new()
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
            Language     = language,
            Branches     = branches ?? [],
        },
    };
}
