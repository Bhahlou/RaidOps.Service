using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Guilds.AuditLog.Queries;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Guilds.AuditLog.QueryHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Guilds.AuditLog.QueryHandlers;

public class GetGuildAuditLogQueryHandlerTests
{
    private readonly Mock<IGuildAccessService>      _access     = new();
    private readonly Mock<IGuildAuditLogRepository> _auditLog   = new();
    private readonly Mock<IUsersRepository>         _users      = new();
    private readonly GetGuildAuditLogQueryHandler   _sut;

    private const string GuildId     = "guild-1";
    private const string RequesterId = "user-1";

    private static readonly GetGuildAuditLogQuery Query = new()
    {
        GuildId            = GuildId,
        RequesterDiscordId = RequesterId,
        Page               = 1,
        PageSize           = 2,
    };

    public GetGuildAuditLogQueryHandlerTests()
    {
        _sut = new GetGuildAuditLogQueryHandler(_access.Object, _auditLog.Object, _users.Object);
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
