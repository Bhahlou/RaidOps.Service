using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Roster.Queries;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Roster.QueryHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Roster.QueryHandlers;

public class GetUnassignedGuildMembersQueryHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IGuildsRepository> _guildsRepository = new();
    private readonly Mock<IGuildMembershipRepository> _guildMembershipRepository = new();
    private readonly Mock<IRaidCompositionRepository> _raidCompositionRepository = new();
    private readonly Mock<IUsersRepository> _usersRepository = new();
    private readonly GetUnassignedGuildMembersQueryHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "roster-1";

    public GetUnassignedGuildMembersQueryHandlerTests()
    {
        _sut = new GetUnassignedGuildMembersQueryHandler(_access.Object, _guildsRepository.Object, _guildMembershipRepository.Object, _raidCompositionRepository.Object, _usersRepository.Object);
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _guildsRepository.Setup(r => r.GetByIdAsync(GuildId, default)).ReturnsAsync(new Guild { Id = GuildId, Timezone = null });
        _raidCompositionRepository.Setup(r => r.GetAssignedCharacterIdsInRangeAsync(GuildBranchId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), default)).ReturnsAsync([]);
        _usersRepository.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), default)).ReturnsAsync([]);
    }

    private static GetUnassignedGuildMembersQuery MakeQuery() => new()
    {
        GuildId = GuildId,
        GuildBranchId = GuildBranchId,
        RequesterDiscordId = RequesterId,
        RangeStart = new DateOnly(2026, 2, 1),
        RangeEnd = new DateOnly(2026, 2, 7),
    };

    private static Character MakeCharacter(int id, string name, string playerDiscordId, CharacterRank rank = CharacterRank.Main) => new()
    {
        Id = id,
        Name = name,
        UserDiscordId = playerDiscordId,
        Class = new WowClass { Id = 1, Name = "Warrior", Color = "C79C6E" },
        Branch = new Branch { Id = 1, Name = "Classic Era" },
        RaidSpecs = [],
    };

    private static GuildMembership MakeMembership(Character character, CharacterRank rank = CharacterRank.Main) => new()
    {
        CharacterId = character.Id,
        GuildId = GuildId,
        GuildBranchId = GuildBranchId,
        CharacterRank = rank,
        Character = character,
    };

    [Fact]
    public async Task HandleAsync_BelowRosterAccess_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Public);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_RangeEndBeforeRangeStart_ReturnsInvalidRequest()
    {
        var query = MakeQuery();
        query.RangeEnd = query.RangeStart.AddDays(-1);

        var result = await _sut.HandleAsync(query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidRequest);
    }

    [Fact]
    public async Task HandleAsync_GuildNotFound_ReturnsGuildNotFound()
    {
        _guildsRepository.Setup(r => r.GetByIdAsync(GuildId, default)).ReturnsAsync((Guild?)null);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildNotFound);
    }

    [Fact]
    public async Task HandleAsync_EveryMemberAssigned_ReturnsEmptyList()
    {
        var character = MakeCharacter(1, "Arthas", "player-1");
        _guildMembershipRepository.Setup(r => r.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync([MakeMembership(character)]);
        _raidCompositionRepository.Setup(r => r.GetAssignedCharacterIdsInRangeAsync(GuildBranchId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), default)).ReturnsAsync([1]);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_UnassignedMembers_AreReturnedSortedByRankThenName()
    {
        var alt = MakeCharacter(1, "Zed", "player-1");
        var mainA = MakeCharacter(2, "Bob", "player-2");
        var mainB = MakeCharacter(3, "Alice", "player-3");
        _guildMembershipRepository.Setup(r => r.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync([
            MakeMembership(alt, CharacterRank.Alt),
            MakeMembership(mainA, CharacterRank.Main),
            MakeMembership(mainB, CharacterRank.Main),
        ]);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
        result.Value!.Select(m => m.CharacterName).Should().ContainInOrder("Alice", "Bob", "Zed");
    }

    [Fact]
    public async Task HandleAsync_AssignedCharacterIsExcluded()
    {
        var assigned = MakeCharacter(1, "Arthas", "player-1");
        var unassigned = MakeCharacter(2, "Jaina", "player-2");
        _guildMembershipRepository.Setup(r => r.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync([MakeMembership(assigned), MakeMembership(unassigned)]);
        _raidCompositionRepository.Setup(r => r.GetAssignedCharacterIdsInRangeAsync(GuildBranchId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), default)).ReturnsAsync([1]);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(m => m.CharacterId == 2);
    }

    [Fact]
    public async Task HandleAsync_PlayerNameResolved_WhenUserExists()
    {
        var character = MakeCharacter(1, "Arthas", "player-1");
        _guildMembershipRepository.Setup(r => r.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync([MakeMembership(character)]);
        _usersRepository.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), default))
            .ReturnsAsync([new User { DiscordId = "player-1", Name = "PlayerOne" }]);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Single().PlayerName.Should().Be("PlayerOne");
    }

    [Fact]
    public async Task HandleAsync_PlayerNotResolved_PlayerNameIsNull()
    {
        var character = MakeCharacter(1, "Arthas", "player-1");
        _guildMembershipRepository.Setup(r => r.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync([MakeMembership(character)]);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Single().PlayerName.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_RaidSpecsOrderedWithMainFirst()
    {
        var character = MakeCharacter(1, "Arthas", "player-1");
        character.RaidSpecs =
        [
            new CharacterRaidSpec { CharacterId = 1, SpecId = 2, IsMain = false, Spec = new Spec { Id = 2, Name = "Fury" } },
            new CharacterRaidSpec { CharacterId = 1, SpecId = 1, IsMain = true, Spec = new Spec { Id = 1, Name = "Arms" } },
        ];
        _guildMembershipRepository.Setup(r => r.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync([MakeMembership(character)]);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Single().RaidSpecs.Select(s => s.Name).Should().ContainInOrder("Arms", "Fury");
    }

    [Fact]
    public async Task HandleAsync_MapsClassAndBranchFields()
    {
        var character = MakeCharacter(1, "Arthas", "player-1");
        character.ClassId = 1;
        character.BranchId = 1;
        character.AvatarUrl = "https://example.com/avatar.png";
        _guildMembershipRepository.Setup(r => r.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync([MakeMembership(character)]);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        var member = result.Value!.Single();
        member.ClassId.Should().Be(1);
        member.ClassName.Should().Be("Warrior");
        member.ClassColor.Should().Be("#C79C6E");
        member.BranchId.Should().Be(1);
        member.BranchName.Should().Be("Classic Era");
        member.AvatarUrl.Should().Be("https://example.com/avatar.png");
    }
}
