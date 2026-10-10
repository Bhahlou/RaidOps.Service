using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Guilds.Roster.Queries;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Guilds.Roster.QueryHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Reference;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;
using RaidOps.UnitTests.ExternalApplication.Bot;

namespace RaidOps.UnitTests.Application.Guilds.Roster.QueryHandlers;

/// <summary>
/// Unit tests for <see cref="GetGuildRosterQueryHandler"/>.
/// </summary>
public class GetGuildRosterQueryHandlerTests
{
    private readonly Mock<IGuildsRepository>          _guilds        = new();
    private readonly Mock<IGuildBranchesRepository>   _guildBranches = new();
    private readonly Mock<IGuildAccessService>        _access        = new();
    private readonly Mock<IGuildMembershipRepository> _memberships   = new();
    private readonly Mock<IUsersRepository>           _users         = new();
    private readonly Mock<IDiscordBotService>         _bot           = new();
    private readonly Mock<IGuildService>              _guildService  = new();
    private readonly GetGuildRosterQueryHandler       _sut;

    private const string GuildId       = "guild-1";
    private const string RequesterId   = "user-1";
    private const int    GuildBranchId = 1;
    private const ulong  OwnerUlong    = 300000000000000001UL;
    private const ulong  GuildUlong    = 900000000000000001UL;

    private static readonly GetGuildRosterQuery Query = new()
    {
        GuildId            = GuildId,
        GuildBranchId      = GuildBranchId,
        RequesterDiscordId = RequesterId,
    };

    public GetGuildRosterQueryHandlerTests()
    {
        _bot.Setup(b => b.Guilds).Returns(_guildService.Object);
        // Default: bot doesn't see the owner in its Gateway cache — every existing test that
        // doesn't care about live member resolution keeps exercising the DB-fallback path.
        _guildService.Setup(g => g.GetUser(GuildId, It.IsAny<string>(), default)).Returns((NetCord.GuildUser?)null);
        _sut = new GetGuildRosterQueryHandler(_guilds.Object, _guildBranches.Object, _access.Object, _memberships.Object, _users.Object, _bot.Object);
        _users.Setup(u => u.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), default))
            .ReturnsAsync([]);
    }

    private static GuildBranch MakeBranch(bool isActive = true) => new()
    {
        Id = GuildBranchId, GuildId = GuildId, BranchId = 1, IsActive = isActive,
    };

    [Fact]
    public async Task HandleAsync_GuildNotFound_ReturnsGuildNotFound()
    {
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync((Guild?)null);

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildNotFound);
    }

    [Fact]
    public async Task HandleAsync_GuildNotRegistered_ReturnsGuildNotFound()
    {
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = false });

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildNotFound);
    }

    [Fact]
    public async Task HandleAsync_BranchNotFound_ReturnsGuildBranchNotFound()
    {
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true });
        _guildBranches.Setup(b => b.GetByIdAsync(GuildBranchId, default)).ReturnsAsync((GuildBranch?)null);

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildBranchNotFound);
    }

    [Fact]
    public async Task HandleAsync_BranchInactive_ReturnsGuildBranchNotFound()
    {
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true });
        _guildBranches.Setup(b => b.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(MakeBranch(isActive: false));

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildBranchNotFound);
    }

    [Fact]
    public async Task HandleAsync_RequesterBelowRosterAccess_ReturnsRosterAccessDenied()
    {
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true });
        _guildBranches.Setup(b => b.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(MakeBranch());
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Public);

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RosterAccessDenied);
    }

    [Fact]
    public async Task HandleAsync_Success_ReturnsMembersOrderedByRankThenName()
    {
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true });
        _guildBranches.Setup(b => b.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(MakeBranch());
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _memberships.Setup(m => m.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync(
        [
            BuildMembership(id: 1, name: "Zed", rank: CharacterRank.Main),
            BuildMembership(id: 2, name: "Aaron", rank: CharacterRank.Alt),
            BuildMembership(id: 3, name: "Bob", rank: CharacterRank.Main),
        ]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Select(m => m.CharacterName).Should().Equal("Bob", "Zed", "Aaron");
    }

    [Fact]
    public async Task HandleAsync_Success_MapsCharacterAndPlayerFields()
    {
        const string ownerId = "owner-1";
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true });
        _guildBranches.Setup(b => b.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(MakeBranch());
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _memberships.Setup(m => m.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync(
        [
            BuildMembership(id: 1, name: "Arthas", rank: CharacterRank.Split, ownerId: ownerId),
        ]);
        _users.Setup(u => u.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), default))
            .ReturnsAsync([new User { DiscordId = ownerId, Name = "Bhahlou", AvatarHash = "hash123" }]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        var member = result.Value!.Single();
        member.CharacterId.Should().Be(1);
        member.ClassName.Should().Be("Death Knight");
        member.ClassColor.Should().Be("#C41F3B");
        member.BranchName.Should().Be("Classic Anniversary");
        member.RealmSlug.Should().Be("kazzak");
        member.Level.Should().Be(80);
        member.RaidSpecs.Should().ContainSingle(s => s.Name == "Frost" && s.IsMain);
        member.PlayerDiscordId.Should().Be(ownerId);
        member.PlayerName.Should().Be("Bhahlou");
        member.PlayerAvatarHash.Should().Be("hash123");
        member.CharacterRank.Should().Be(CharacterRank.Split);
    }

    // ── Guild-local identity (Gateway cache) ────────────────────────────────

    [Fact]
    public async Task HandleAsync_OwnerFoundInGatewayCache_PrefersGuildNicknameOverDbName()
    {
        const string ownerId = "owner-1";
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true });
        _guildBranches.Setup(b => b.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(MakeBranch());
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _memberships.Setup(m => m.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync(
        [
            BuildMembership(id: 1, name: "Arthas", rank: CharacterRank.Main, ownerId: ownerId),
        ]);
        _users.Setup(u => u.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), default))
            .ReturnsAsync([new User { DiscordId = ownerId, Name = "Bhahlou", AvatarHash = "stale-hash" }]);
        _guildService.Setup(g => g.GetUser(GuildId, ownerId, default))
            .Returns(NetCordTestHelpers.MakeGuildUser(OwnerUlong, GuildUlong, [], username: "bhahlou", nickname: "Le Boss", avatarHash: "live-hash"));

        var result = await _sut.HandleAsync(Query, default);

        result.Value!.Single().PlayerName.Should().Be("Le Boss");
    }

    [Fact]
    public async Task HandleAsync_OwnerFoundInGatewayCache_PrefersLiveAvatarHashOverStaleDbSnapshot()
    {
        const string ownerId = "owner-1";
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true });
        _guildBranches.Setup(b => b.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(MakeBranch());
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _memberships.Setup(m => m.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync(
        [
            BuildMembership(id: 1, name: "Arthas", rank: CharacterRank.Main, ownerId: ownerId),
        ]);
        _users.Setup(u => u.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), default))
            .ReturnsAsync([new User { DiscordId = ownerId, Name = "Bhahlou", AvatarHash = "stale-hash" }]);
        _guildService.Setup(g => g.GetUser(GuildId, ownerId, default))
            .Returns(NetCordTestHelpers.MakeGuildUser(OwnerUlong, GuildUlong, [], username: "bhahlou", avatarHash: "live-hash"));

        var result = await _sut.HandleAsync(Query, default);

        result.Value!.Single().PlayerAvatarHash.Should().Be("live-hash");
    }

    [Fact]
    public async Task HandleAsync_OwnerFoundInGatewayCacheWithNoAvatar_DoesNotFallBackToStaleDbHash()
    {
        // Regression test: the owner removed their Discord avatar entirely, but the DB's User row
        // (only resynced at login/token-refresh) still holds the old hash. The live Gateway lookup
        // finding the member with no avatar must win — null is meaningful, not "unknown".
        const string ownerId = "owner-1";
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true });
        _guildBranches.Setup(b => b.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(MakeBranch());
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _memberships.Setup(m => m.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync(
        [
            BuildMembership(id: 1, name: "Arthas", rank: CharacterRank.Main, ownerId: ownerId),
        ]);
        _users.Setup(u => u.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), default))
            .ReturnsAsync([new User { DiscordId = ownerId, Name = "Bhahlou", AvatarHash = "stale-hash" }]);
        _guildService.Setup(g => g.GetUser(GuildId, ownerId, default))
            .Returns(NetCordTestHelpers.MakeGuildUser(OwnerUlong, GuildUlong, [], username: "bhahlou"));

        var result = await _sut.HandleAsync(Query, default);

        result.Value!.Single().PlayerAvatarHash.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_OwnerHasGuildAvatarOverride_PopulatesPlayerGuildAvatarUrl()
    {
        const string ownerId = "owner-1";
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true });
        _guildBranches.Setup(b => b.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(MakeBranch());
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _memberships.Setup(m => m.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync(
        [
            BuildMembership(id: 1, name: "Arthas", rank: CharacterRank.Main, ownerId: ownerId),
        ]);
        _guildService.Setup(g => g.GetUser(GuildId, ownerId, default))
            .Returns(NetCordTestHelpers.MakeGuildUser(OwnerUlong, GuildUlong, [], username: "bhahlou", guildAvatarHash: "guild-hash"));

        var result = await _sut.HandleAsync(Query, default);

        result.Value!.Single().PlayerGuildAvatarUrl.Should().Be($"https://cdn.discordapp.com/guilds/{GuildUlong}/users/{OwnerUlong}/avatars/guild-hash.png");
    }

    [Fact]
    public async Task HandleAsync_OwnerHasNoGuildAvatarOverride_PlayerGuildAvatarUrlIsNull()
    {
        const string ownerId = "owner-1";
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true });
        _guildBranches.Setup(b => b.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(MakeBranch());
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _memberships.Setup(m => m.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync(
        [
            BuildMembership(id: 1, name: "Arthas", rank: CharacterRank.Main, ownerId: ownerId),
        ]);
        _guildService.Setup(g => g.GetUser(GuildId, ownerId, default))
            .Returns(NetCordTestHelpers.MakeGuildUser(OwnerUlong, GuildUlong, [], username: "bhahlou", avatarHash: "live-hash"));

        var result = await _sut.HandleAsync(Query, default);

        result.Value!.Single().PlayerGuildAvatarUrl.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_BotNotInGuild_FallsBackToDbSnapshotWithoutThrowing()
    {
        const string ownerId = "owner-1";
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true });
        _guildBranches.Setup(b => b.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(MakeBranch());
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _memberships.Setup(m => m.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync(
        [
            BuildMembership(id: 1, name: "Arthas", rank: CharacterRank.Main, ownerId: ownerId),
        ]);
        _users.Setup(u => u.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), default))
            .ReturnsAsync([new User { DiscordId = ownerId, Name = "Bhahlou", AvatarHash = "db-hash" }]);
        _guildService.Setup(g => g.GetUser(GuildId, ownerId, default)).Throws<InvalidOperationException>();

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        var member = result.Value!.Single();
        member.PlayerName.Should().Be("Bhahlou");
        member.PlayerAvatarHash.Should().Be("db-hash");
        member.PlayerGuildAvatarUrl.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_Success_NoActiveExpansionState_FallsBackToHighestLevel()
    {
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true });
        _guildBranches.Setup(b => b.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(MakeBranch());
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        var membership = BuildMembership(id: 1, name: "Arthas", rank: CharacterRank.Main);
        // No expansion state is marked IsActive — the character never activated a version yet,
        // so the mapper must fall back to the highest-level state instead of returning Level 0.
        membership.Character.ExpansionStates =
        [
            new CharacterExpansionState { CharacterId = 1, IsActive = false, Level = 60, ItemLevel = 200 },
            new CharacterExpansionState { CharacterId = 1, IsActive = false, Level = 80, ItemLevel = 620 },
        ];
        _memberships.Setup(m => m.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync([membership]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Single().Level.Should().Be(80);
    }

    [Fact]
    public async Task HandleAsync_Success_NoExpansionStatesAtAll_LevelDefaultsToZero()
    {
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true });
        _guildBranches.Setup(b => b.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(MakeBranch());
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        var membership = BuildMembership(id: 1, name: "Arthas", rank: CharacterRank.Main);
        // A never-synced character has no expansion state rows at all — activeState resolves to
        // null, exercising the `?? 0` default rather than the fallback-by-level path.
        membership.Character.ExpansionStates = [];
        _memberships.Setup(m => m.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync([membership]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Single().Level.Should().Be(0);
    }

    // ── CanExclude ────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_OfficerRequester_OutranksTarget_SetsCanExcludeTrue()
    {
        const string ownerId = "owner-1";
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true });
        _guildBranches.Setup(b => b.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(MakeBranch());
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _access.Setup(a => a.OutranksAsync(GuildId, GuildBranchId, RequesterId, ownerId, default)).ReturnsAsync(true);
        _memberships.Setup(m => m.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync(
        [
            BuildMembership(id: 1, name: "Arthas", rank: CharacterRank.Main, ownerId: ownerId),
        ]);

        var result = await _sut.HandleAsync(Query, default);

        result.Value!.Single().CanExclude.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_OfficerRequester_DoesNotOutrankTarget_SetsCanExcludeFalse()
    {
        const string ownerId = "owner-1";
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true });
        _guildBranches.Setup(b => b.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(MakeBranch());
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _access.Setup(a => a.OutranksAsync(GuildId, GuildBranchId, RequesterId, ownerId, default)).ReturnsAsync(false);
        _memberships.Setup(m => m.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync(
        [
            BuildMembership(id: 1, name: "Arthas", rank: CharacterRank.Main, ownerId: ownerId),
        ]);

        var result = await _sut.HandleAsync(Query, default);

        result.Value!.Single().CanExclude.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_OfficerRequester_OwnRow_SetsCanExcludeTrueWithoutCallingOutranks()
    {
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true });
        _guildBranches.Setup(b => b.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(MakeBranch());
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _memberships.Setup(m => m.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync(
        [
            BuildMembership(id: 1, name: "Arthas", rank: CharacterRank.Main, ownerId: RequesterId),
        ]);

        var result = await _sut.HandleAsync(Query, default);

        result.Value!.Single().CanExclude.Should().BeTrue();
        _access.Verify(a => a.OutranksAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_RosterOnlyRequester_SetsCanExcludeFalseForAllRows()
    {
        const string ownerId = "owner-1";
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true });
        _guildBranches.Setup(b => b.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(MakeBranch());
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _memberships.Setup(m => m.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync(
        [
            BuildMembership(id: 1, name: "Arthas", rank: CharacterRank.Main, ownerId: RequesterId),
            BuildMembership(id: 2, name: "Jaina", rank: CharacterRank.Main, ownerId: ownerId),
        ]);

        var result = await _sut.HandleAsync(Query, default);

        result.Value!.Should().OnlyContain(m => !m.CanExclude);
        _access.Verify(a => a.OutranksAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    private static GuildMembership BuildMembership(int id, string name, CharacterRank rank, string ownerId = "owner-1")
    {
        var frostSpec = new Spec { Id = 1, Name = "Frost" };
        return new GuildMembership
        {
            CharacterId   = id,
            GuildId       = GuildId,
            GuildBranchId = GuildBranchId,
            CharacterRank = rank,
            JoinedAt      = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Character = new Character
            {
                Id            = id,
                Name          = name,
                UserDiscordId = ownerId,
                ClassId       = 6,
                Class         = new WowClass { Id = 6, Name = "Death Knight", Color = "C41F3B" },
                RealmId       = 1,
                Realm         = new Realm { Id = 1, Name = "Kazzak", Slug = "kazzak", Region = "eu" },
                BranchId      = 1,
                Branch        = new Branch { Id = 1, Name = "Classic Anniversary" },
                RaidSpecs     = [new CharacterRaidSpec { CharacterId = id, SpecId = 1, IsMain = true, Spec = frostSpec }],
                ExpansionStates =
                [
                    new CharacterExpansionState
                    {
                        CharacterId = id, IsActive = true, Level = 80, ItemLevel = 620,
                    },
                ],
            },
        };
    }
}
