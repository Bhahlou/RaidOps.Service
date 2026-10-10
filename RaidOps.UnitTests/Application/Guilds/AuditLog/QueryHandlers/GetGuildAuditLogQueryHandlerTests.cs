using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Guilds.AuditLog.Queries;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Guilds.AuditLog.QueryHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;
using RaidOps.UnitTests.ExternalApplication.Bot;

namespace RaidOps.UnitTests.Application.Guilds.AuditLog.QueryHandlers;

public class GetGuildAuditLogQueryHandlerTests
{
    private readonly Mock<IGuildAccessService>      _access       = new();
    private readonly Mock<IGuildAuditLogRepository> _auditLog     = new();
    private readonly Mock<IUsersRepository>         _users        = new();
    private readonly Mock<IDiscordBotService>       _bot          = new();
    private readonly Mock<IGuildService>            _guildService = new();
    private readonly GetGuildAuditLogQueryHandler   _sut;

    private const string GuildId     = "guild-1";
    private const string RequesterId = "user-1";
    private const ulong  ActorUlong  = 300000000000000001UL;
    private const ulong  GuildUlong  = 900000000000000001UL;

    private static readonly GetGuildAuditLogQuery Query = new()
    {
        GuildId            = GuildId,
        RequesterDiscordId = RequesterId,
        Page               = 1,
        PageSize           = 2,
    };

    public GetGuildAuditLogQueryHandlerTests()
    {
        _bot.Setup(b => b.Guilds).Returns(_guildService.Object);
        // Default: bot doesn't see the actor in its Gateway cache — every existing test that
        // doesn't care about live member resolution keeps exercising the DB-fallback path.
        _guildService.Setup(g => g.GetUser(GuildId, It.IsAny<string>(), default)).Returns((NetCord.GuildUser?)null);
        _sut = new GetGuildAuditLogQueryHandler(_access.Object, _auditLog.Object, _users.Object, _bot.Object);
    }

    private void SetupAdmin(bool isAdmin = true) =>
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default))
            .ReturnsAsync(isAdmin ? GuildAccessLevel.Officer : GuildAccessLevel.Roster);

    [Fact]
    public async Task HandleAsync_RequesterNotMember_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.None);

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_RequesterNotAdmin_ReturnsForbidden()
    {
        SetupAdmin(isAdmin: false);

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_FewerEntriesThanPageSizePlusOne_HasMoreIsFalse()
    {
        SetupAdmin();
        _auditLog.Setup(r => r.GetPagedByGuildIdAsync(GuildId, 0, 3, null, default))
            .ReturnsAsync([
                new GuildAuditLog { Id = 1, GuildId = GuildId, ActorDiscordId = RequesterId, ActionType = GuildAuditAction.GuildRegistered, OccurredAt = DateTime.UtcNow },
            ]);
        _users.Setup(u => u.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), default))
            .ReturnsAsync([]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value?.HasMore.Should().BeFalse();
        result.Value?.Entries.Should().HaveCount(1);
    }

    [Fact]
    public async Task HandleAsync_MoreEntriesThanPageSize_HasMoreIsTrueAndTrimmedToPageSize()
    {
        SetupAdmin();
        _auditLog.Setup(r => r.GetPagedByGuildIdAsync(GuildId, 0, 3, null, default))
            .ReturnsAsync([
                new GuildAuditLog { Id = 1, GuildId = GuildId, ActorDiscordId = RequesterId, ActionType = GuildAuditAction.GuildRegistered, OccurredAt = DateTime.UtcNow },
                new GuildAuditLog { Id = 2, GuildId = GuildId, ActorDiscordId = RequesterId, ActionType = GuildAuditAction.GuildRegistered, OccurredAt = DateTime.UtcNow },
                new GuildAuditLog { Id = 3, GuildId = GuildId, ActorDiscordId = RequesterId, ActionType = GuildAuditAction.GuildRegistered, OccurredAt = DateTime.UtcNow },
            ]);
        _users.Setup(u => u.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), default))
            .ReturnsAsync([]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value?.HasMore.Should().BeTrue();
        result.Value?.Entries.Should().HaveCount(2);
    }

    [Fact]
    public async Task HandleAsync_ActionTypeFilterSet_PassesItThroughToRepository()
    {
        SetupAdmin();
        var query = new GetGuildAuditLogQuery
        {
            GuildId            = GuildId,
            RequesterDiscordId = RequesterId,
            Page               = 1,
            PageSize           = 2,
            ActionType         = GuildAuditAction.MemberRankUpdated,
        };
        _auditLog.Setup(r => r.GetPagedByGuildIdAsync(GuildId, 0, 3, new[] { GuildAuditAction.MemberRankUpdated }, default))
            .ReturnsAsync([]);
        _users.Setup(u => u.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), default))
            .ReturnsAsync([]);

        var result = await _sut.HandleAsync(query, default);

        result.IsSuccess.Should().BeTrue();
        _auditLog.Verify(r => r.GetPagedByGuildIdAsync(GuildId, 0, 3, new[] { GuildAuditAction.MemberRankUpdated }, default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_CategorySet_TranslatesToMatchingActionTypes()
    {
        SetupAdmin();
        var query = new GetGuildAuditLogQuery
        {
            GuildId = GuildId, RequesterDiscordId = RequesterId, Page = 1, PageSize = 2, Category = GuildAuditCategory.Roster,
        };
        _auditLog.Setup(r => r.GetPagedByGuildIdAsync(
                GuildId, 0, 3,
                It.Is<IReadOnlyCollection<GuildAuditAction>>(a => a.Count == 4
                    && a.Contains(GuildAuditAction.MemberJoined)
                    && a.Contains(GuildAuditAction.MemberLeft)
                    && a.Contains(GuildAuditAction.MemberExcluded)
                    && a.Contains(GuildAuditAction.MemberRankUpdated)),
                default))
            .ReturnsAsync([]);
        _users.Setup(u => u.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), default))
            .ReturnsAsync([]);

        var result = await _sut.HandleAsync(query, default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_ActionTypeAndCategoryBothSet_ActionTypeTakesPrecedence()
    {
        SetupAdmin();
        var query = new GetGuildAuditLogQuery
        {
            GuildId = GuildId, RequesterDiscordId = RequesterId, Page = 1, PageSize = 2,
            ActionType = GuildAuditAction.MemberRankUpdated, Category = GuildAuditCategory.Settings,
        };
        _auditLog.Setup(r => r.GetPagedByGuildIdAsync(GuildId, 0, 3, new[] { GuildAuditAction.MemberRankUpdated }, default))
            .ReturnsAsync([]);
        _users.Setup(u => u.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), default))
            .ReturnsAsync([]);

        var result = await _sut.HandleAsync(query, default);

        result.IsSuccess.Should().BeTrue();
        _auditLog.Verify(r => r.GetPagedByGuildIdAsync(GuildId, 0, 3, new[] { GuildAuditAction.MemberRankUpdated }, default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_SecondPage_ComputesCorrectSkip()
    {
        SetupAdmin();
        var query = new GetGuildAuditLogQuery { GuildId = GuildId, RequesterDiscordId = RequesterId, Page = 3, PageSize = 2 };
        _auditLog.Setup(r => r.GetPagedByGuildIdAsync(GuildId, 4, 3, null, default)).ReturnsAsync([]);
        _users.Setup(u => u.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), default))
            .ReturnsAsync([]);

        var result = await _sut.HandleAsync(query, default);

        result.IsSuccess.Should().BeTrue();
        _auditLog.Verify(r => r.GetPagedByGuildIdAsync(GuildId, 4, 3, null, default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ActorFoundInUsersRepository_PopulatesUsernameAndAvatar()
    {
        SetupAdmin();
        _auditLog.Setup(r => r.GetPagedByGuildIdAsync(GuildId, 0, 3, null, default))
            .ReturnsAsync([
                new GuildAuditLog { Id = 1, GuildId = GuildId, ActorDiscordId = "actor-1", ActionType = GuildAuditAction.GuildRegistered, OccurredAt = DateTime.UtcNow },
            ]);
        _users.Setup(u => u.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), default))
            .ReturnsAsync([new User { DiscordId = "actor-1", Name = "Bhahlou", AvatarHash = "abc123", RefreshToken = "x", LastRefresh = DateTimeOffset.UtcNow }]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value?.Entries[0].ActorUsername.Should().Be("Bhahlou");
        result.Value?.Entries[0].ActorAvatarHash.Should().Be("abc123");
    }

    [Fact]
    public async Task HandleAsync_ActorNotFoundInUsersRepository_LeavesUsernameAndAvatarNull()
    {
        SetupAdmin();
        _auditLog.Setup(r => r.GetPagedByGuildIdAsync(GuildId, 0, 3, null, default))
            .ReturnsAsync([
                new GuildAuditLog { Id = 1, GuildId = GuildId, ActorDiscordId = "unknown-actor", ActionType = GuildAuditAction.GuildRegistered, OccurredAt = DateTime.UtcNow },
            ]);
        _users.Setup(u => u.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), default))
            .ReturnsAsync([]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value?.Entries[0].ActorUsername.Should().BeNull();
        result.Value?.Entries[0].ActorAvatarHash.Should().BeNull();
    }

    // ── Guild-local identity (Gateway cache) ────────────────────────────────

    [Fact]
    public async Task HandleAsync_ActorFoundInGatewayCache_PrefersGuildNicknameOverDbName()
    {
        SetupAdmin();
        _auditLog.Setup(r => r.GetPagedByGuildIdAsync(GuildId, 0, 3, null, default))
            .ReturnsAsync([
                new GuildAuditLog { Id = 1, GuildId = GuildId, ActorDiscordId = "actor-1", ActionType = GuildAuditAction.GuildRegistered, OccurredAt = DateTime.UtcNow },
            ]);
        _users.Setup(u => u.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), default))
            .ReturnsAsync([new User { DiscordId = "actor-1", Name = "Bhahlou", AvatarHash = "stale-hash", RefreshToken = "x", LastRefresh = DateTimeOffset.UtcNow }]);
        _guildService.Setup(g => g.GetUser(GuildId, "actor-1", default))
            .Returns(NetCordTestHelpers.MakeGuildUser(ActorUlong, GuildUlong, [], username: "bhahlou", nickname: "Le Boss", avatarHash: "live-hash"));

        var result = await _sut.HandleAsync(Query, default);

        result.Value?.Entries[0].ActorUsername.Should().Be("Le Boss");
    }

    [Fact]
    public async Task HandleAsync_ActorFoundInGatewayCache_PrefersLiveAvatarHashOverStaleDbSnapshot()
    {
        SetupAdmin();
        _auditLog.Setup(r => r.GetPagedByGuildIdAsync(GuildId, 0, 3, null, default))
            .ReturnsAsync([
                new GuildAuditLog { Id = 1, GuildId = GuildId, ActorDiscordId = "actor-1", ActionType = GuildAuditAction.GuildRegistered, OccurredAt = DateTime.UtcNow },
            ]);
        _users.Setup(u => u.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), default))
            .ReturnsAsync([new User { DiscordId = "actor-1", Name = "Bhahlou", AvatarHash = "stale-hash", RefreshToken = "x", LastRefresh = DateTimeOffset.UtcNow }]);
        _guildService.Setup(g => g.GetUser(GuildId, "actor-1", default))
            .Returns(NetCordTestHelpers.MakeGuildUser(ActorUlong, GuildUlong, [], username: "bhahlou", avatarHash: "live-hash"));

        var result = await _sut.HandleAsync(Query, default);

        result.Value?.Entries[0].ActorAvatarHash.Should().Be("live-hash");
    }

    [Fact]
    public async Task HandleAsync_ActorFoundInGatewayCacheWithNoAvatar_DoesNotFallBackToStaleDbHash()
    {
        // Regression test: the actor removed their Discord avatar entirely, but the DB's User
        // row (only resynced at login/token-refresh) still holds the old hash. The live Gateway
        // lookup finding the member with no avatar must win — null is meaningful, not "unknown".
        SetupAdmin();
        _auditLog.Setup(r => r.GetPagedByGuildIdAsync(GuildId, 0, 3, null, default))
            .ReturnsAsync([
                new GuildAuditLog { Id = 1, GuildId = GuildId, ActorDiscordId = "actor-1", ActionType = GuildAuditAction.GuildRegistered, OccurredAt = DateTime.UtcNow },
            ]);
        _users.Setup(u => u.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), default))
            .ReturnsAsync([new User { DiscordId = "actor-1", Name = "Bhahlou", AvatarHash = "stale-hash", RefreshToken = "x", LastRefresh = DateTimeOffset.UtcNow }]);
        _guildService.Setup(g => g.GetUser(GuildId, "actor-1", default))
            .Returns(NetCordTestHelpers.MakeGuildUser(ActorUlong, GuildUlong, [], username: "bhahlou"));

        var result = await _sut.HandleAsync(Query, default);

        result.Value?.Entries[0].ActorAvatarHash.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_ActorHasGuildAvatarOverride_PopulatesActorGuildAvatarUrl()
    {
        SetupAdmin();
        _auditLog.Setup(r => r.GetPagedByGuildIdAsync(GuildId, 0, 3, null, default))
            .ReturnsAsync([
                new GuildAuditLog { Id = 1, GuildId = GuildId, ActorDiscordId = "actor-1", ActionType = GuildAuditAction.GuildRegistered, OccurredAt = DateTime.UtcNow },
            ]);
        _users.Setup(u => u.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), default))
            .ReturnsAsync([]);
        _guildService.Setup(g => g.GetUser(GuildId, "actor-1", default))
            .Returns(NetCordTestHelpers.MakeGuildUser(ActorUlong, GuildUlong, [], username: "bhahlou", guildAvatarHash: "guild-hash"));

        var result = await _sut.HandleAsync(Query, default);

        result.Value?.Entries[0].ActorGuildAvatarUrl.Should().Be($"https://cdn.discordapp.com/guilds/{GuildUlong}/users/{ActorUlong}/avatars/guild-hash.png");
    }

    [Fact]
    public async Task HandleAsync_ActorHasNoGuildAvatarOverride_ActorGuildAvatarUrlIsNull()
    {
        SetupAdmin();
        _auditLog.Setup(r => r.GetPagedByGuildIdAsync(GuildId, 0, 3, null, default))
            .ReturnsAsync([
                new GuildAuditLog { Id = 1, GuildId = GuildId, ActorDiscordId = "actor-1", ActionType = GuildAuditAction.GuildRegistered, OccurredAt = DateTime.UtcNow },
            ]);
        _users.Setup(u => u.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), default))
            .ReturnsAsync([]);
        _guildService.Setup(g => g.GetUser(GuildId, "actor-1", default))
            .Returns(NetCordTestHelpers.MakeGuildUser(ActorUlong, GuildUlong, [], username: "bhahlou", avatarHash: "live-hash"));

        var result = await _sut.HandleAsync(Query, default);

        result.Value?.Entries[0].ActorGuildAvatarUrl.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_BotNotInGuild_FallsBackToDbSnapshotWithoutThrowing()
    {
        SetupAdmin();
        _auditLog.Setup(r => r.GetPagedByGuildIdAsync(GuildId, 0, 3, null, default))
            .ReturnsAsync([
                new GuildAuditLog { Id = 1, GuildId = GuildId, ActorDiscordId = "actor-1", ActionType = GuildAuditAction.GuildRegistered, OccurredAt = DateTime.UtcNow },
            ]);
        _users.Setup(u => u.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), default))
            .ReturnsAsync([new User { DiscordId = "actor-1", Name = "Bhahlou", AvatarHash = "db-hash", RefreshToken = "x", LastRefresh = DateTimeOffset.UtcNow }]);
        _guildService.Setup(g => g.GetUser(GuildId, "actor-1", default)).Throws<InvalidOperationException>();

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value?.Entries[0].ActorUsername.Should().Be("Bhahlou");
        result.Value?.Entries[0].ActorAvatarHash.Should().Be("db-hash");
        result.Value?.Entries[0].ActorGuildAvatarUrl.Should().BeNull();
    }

    [Theory]
    [InlineData(GuildAuditAction.GuildRegistered, GuildAuditCategory.Guild)]
    [InlineData(GuildAuditAction.SettingsUpdated, GuildAuditCategory.Settings)]
    [InlineData(GuildAuditAction.MemberJoined, GuildAuditCategory.Roster)]
    [InlineData(GuildAuditAction.MemberLeft, GuildAuditCategory.Roster)]
    [InlineData(GuildAuditAction.MemberExcluded, GuildAuditCategory.Roster)]
    [InlineData(GuildAuditAction.MemberRankUpdated, GuildAuditCategory.Roster)]
    [InlineData(GuildAuditAction.OfficerThresholdUpdated, GuildAuditCategory.Settings)]
    [InlineData(GuildAuditAction.AvailabilityExceptionDeclared, GuildAuditCategory.Availability)]
    [InlineData(GuildAuditAction.AvailabilityExceptionDeleted, GuildAuditCategory.Availability)]
    [InlineData(GuildAuditAction.RecurringAvailabilityPatternCreated, GuildAuditCategory.Availability)]
    [InlineData(GuildAuditAction.RecurringAvailabilityPatternUpdated, GuildAuditCategory.Availability)]
    [InlineData(GuildAuditAction.RecurringAvailabilityPatternStopped, GuildAuditCategory.Availability)]
    [InlineData(GuildAuditAction.NotificationSettingsUpdated, GuildAuditCategory.Settings)]
    [InlineData(GuildAuditAction.BranchActivated, GuildAuditCategory.Branches)]
    [InlineData(GuildAuditAction.BranchDeactivated, GuildAuditCategory.Branches)]
    [InlineData(GuildAuditAction.BranchRosterSettingsUpdated, GuildAuditCategory.Branches)]
    [InlineData(GuildAuditAction.NotificationSettingsReset, GuildAuditCategory.Settings)]
    [InlineData(GuildAuditAction.RaidSeriesCreated, GuildAuditCategory.Raids)]
    [InlineData(GuildAuditAction.RaidSeriesUpdated, GuildAuditCategory.Raids)]
    [InlineData(GuildAuditAction.RaidSeriesDeactivated, GuildAuditCategory.Raids)]
    [InlineData(GuildAuditAction.RaidEventCreated, GuildAuditCategory.Raids)]
    [InlineData(GuildAuditAction.RaidEventUpdated, GuildAuditCategory.Raids)]
    [InlineData(GuildAuditAction.RaidEventCancelled, GuildAuditCategory.Raids)]
    [InlineData(GuildAuditAction.RaidEventDeleted, GuildAuditCategory.Raids)]
    [InlineData(GuildAuditAction.RaidEventPublished, GuildAuditCategory.Raids)]
    [InlineData(GuildAuditAction.BranchRegionUpdated, GuildAuditCategory.Branches)]
    [InlineData(GuildAuditAction.SlotAssigned, GuildAuditCategory.Raids)]
    [InlineData(GuildAuditAction.SlotUnassigned, GuildAuditCategory.Raids)]
    [InlineData(GuildAuditAction.SlotsSwapped, GuildAuditCategory.Raids)]
    [InlineData(GuildAuditAction.SlotAssignmentSpecChanged, GuildAuditCategory.Raids)]
    public async Task HandleAsync_MapsActionTypeToExpectedCategory(GuildAuditAction actionType, GuildAuditCategory expectedCategory)
    {
        SetupAdmin();
        _auditLog.Setup(r => r.GetPagedByGuildIdAsync(GuildId, 0, 3, null, default))
            .ReturnsAsync([
                new GuildAuditLog { Id = 1, GuildId = GuildId, ActorDiscordId = RequesterId, ActionType = actionType, OccurredAt = DateTime.UtcNow },
            ]);
        _users.Setup(u => u.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), default))
            .ReturnsAsync([]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value?.Entries[0].Category.Should().Be(expectedCategory);
    }

    [Fact]
    public async Task HandleAsync_DetailsIsJson_DeserializesIntoVariables()
    {
        SetupAdmin();
        _auditLog.Setup(r => r.GetPagedByGuildIdAsync(GuildId, 0, 3, null, default))
            .ReturnsAsync([
                new GuildAuditLog
                {
                    Id = 1, GuildId = GuildId, ActorDiscordId = RequesterId,
                    ActionType = GuildAuditAction.MemberJoined,
                    Details = "{\"characterName\":\"Arthas\"}",
                    OccurredAt = DateTime.UtcNow,
                },
            ]);
        _users.Setup(u => u.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), default))
            .ReturnsAsync([]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value?.Entries[0].Variables.Should().ContainKey("characterName").WhoseValue.Should().Be("Arthas");
    }

    [Fact]
    public async Task HandleAsync_DetailsIsNull_VariablesIsNull()
    {
        SetupAdmin();
        _auditLog.Setup(r => r.GetPagedByGuildIdAsync(GuildId, 0, 3, null, default))
            .ReturnsAsync([
                new GuildAuditLog { Id = 1, GuildId = GuildId, ActorDiscordId = RequesterId, ActionType = GuildAuditAction.GuildRegistered, Details = null, OccurredAt = DateTime.UtcNow },
            ]);
        _users.Setup(u => u.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), default))
            .ReturnsAsync([]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value?.Entries[0].Variables.Should().BeNull();
    }
}
