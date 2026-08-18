using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Signups.Queries;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Signups.QueryHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Raids;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Signups.QueryHandlers;

public class GetRaidSignupsQueryHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IRaidEventRepository> _raidEventRepository = new();
    private readonly Mock<IGuildMembershipRepository> _guildMembershipRepository = new();
    private readonly Mock<IRaidSignupRepository> _raidSignupRepository = new();
    private readonly Mock<IUsersRepository> _usersRepository = new();
    private readonly GetRaidSignupsQueryHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "officer-1";
    private const int EventId = 5;

    private static readonly GetRaidSignupsQuery Query = new() { GuildId = GuildId, RequesterDiscordId = RequesterId, GuildBranchId = GuildBranchId, EventId = EventId };

    public GetRaidSignupsQueryHandlerTests()
    {
        _sut = new GetRaidSignupsQueryHandler(_access.Object, _raidEventRepository.Object, _guildMembershipRepository.Object, _raidSignupRepository.Object, _usersRepository.Object);
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent { Id = EventId });
        _guildMembershipRepository.Setup(m => m.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync([]);
        _raidSignupRepository.Setup(r => r.GetForEventAsync(EventId, default)).ReturnsAsync([]);
        _usersRepository.Setup(u => u.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), default)).ReturnsAsync([]);
    }

    private static GuildMembership MakeMembership(string playerDiscordId) => new() { GuildBranchId = GuildBranchId, Character = new Character { UserDiscordId = playerDiscordId } };

    [Fact]
    public async Task HandleAsync_BelowRosterAccess_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Public);

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_RosterAccess_Succeeds()
    {
        // Deliberately Roster, not Officer — GetRaidSignupsQuery was relaxed to any roster member.
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_EventNotFound_ReturnsRaidEventNotFound()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync((RaidEvent?)null);

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidEventNotFound);
    }

    [Fact]
    public async Task HandleAsync_RosterMemberWithNoResponse_ListsThemWithNullStatus()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guildMembershipRepository.Setup(m => m.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync([MakeMembership("player-1")]);
        _usersRepository.Setup(u => u.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), default))
            .ReturnsAsync([new User { DiscordId = "player-1", Name = "Thrall" }]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(r => r.UserDiscordId == "player-1" && r.PlayerName == "Thrall" && r.Status == null && r.CharacterId == null);
    }

    [Fact]
    public async Task HandleAsync_RosterMemberWithAcceptedResponse_MapsCharacterClassAndSpecFields()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guildMembershipRepository.Setup(m => m.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync([MakeMembership("player-1")]);
        _usersRepository.Setup(u => u.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), default))
            .ReturnsAsync([new User { DiscordId = "player-1", Name = "Thrall" }]);

        var respondedAt = new DateTime(2026, 2, 1, 20, 0, 0, DateTimeKind.Utc);
        _raidSignupRepository.Setup(r => r.GetForEventAsync(EventId, default)).ReturnsAsync(
        [
            new RaidSignup
            {
                RaidEventId = EventId,
                UserDiscordId = "player-1",
                Status = SignupStatus.Accepted,
                CharacterId = 42,
                SpecId = 71,
                RespondedAtUtc = respondedAt,
                Character = new Character { Id = 42, Name = "Arthas", ClassId = 1, Class = new WowClass { Id = 1, Name = "Warrior", Color = "C79C6E" } },
                Spec = new Spec { Id = 71, Name = "Arms", IconUrl = "arms.png" },
            },
        ]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        var response = result.Value.Should().ContainSingle().Which;
        response.UserDiscordId.Should().Be("player-1");
        response.Status.Should().Be(SignupStatus.Accepted);
        response.RespondedAtUtc.Should().Be(respondedAt);
        response.CharacterId.Should().Be(42);
        response.CharacterName.Should().Be("Arthas");
        response.ClassId.Should().Be(1);
        response.ClassName.Should().Be("Warrior");
        response.SpecId.Should().Be(71);
        response.SpecName.Should().Be("Arms");
        response.SpecIconUrl.Should().Be("arms.png");
    }

    [Fact]
    public async Task HandleAsync_MultipleMembers_SortedByPlayerNameCaseInsensitive()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guildMembershipRepository.Setup(m => m.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync([MakeMembership("player-a"), MakeMembership("player-b")]);
        _usersRepository.Setup(u => u.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), default))
            .ReturnsAsync([new User { DiscordId = "player-a", Name = "zeta" }, new User { DiscordId = "player-b", Name = "Alpha" }]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Select(r => r.PlayerName).Should().Equal("Alpha", "zeta");
    }

    [Fact]
    public async Task HandleAsync_DuplicateGuildBranchIdsAcrossMemberships_DeduplicatesPlayers()
    {
        // A player with two active characters on the same branch shows up once, not twice.
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guildMembershipRepository.Setup(m => m.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync([MakeMembership("player-1"), MakeMembership("player-1")]);
        _usersRepository.Setup(u => u.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), default))
            .ReturnsAsync([new User { DiscordId = "player-1", Name = "Thrall" }]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
    }
}
