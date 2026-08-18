using FluentAssertions;
using Moq;
using RaidOps.Application.Implementations.Raids.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Raids;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Services;

public class RaidSignupResponseBuilderTests
{
    private readonly Mock<IRaidSignupRepository> _raidSignupRepository = new();
    private readonly Mock<IUsersRepository> _usersRepository = new();
    private readonly Mock<IGuildMembershipRepository> _guildMembershipRepository = new();
    private readonly RaidSignupResponseBuilder _sut;

    private const int GuildBranchId = 10;
    private const int EventId = 5;

    public RaidSignupResponseBuilderTests()
    {
        _sut = new RaidSignupResponseBuilder(_raidSignupRepository.Object, _usersRepository.Object, _guildMembershipRepository.Object);
        _guildMembershipRepository.Setup(m => m.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync([]);
        _raidSignupRepository.Setup(r => r.GetForEventAsync(EventId, default)).ReturnsAsync([]);
        _usersRepository.Setup(u => u.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), default)).ReturnsAsync([]);
    }

    private static GuildMembership MakeMembership(string playerDiscordId) => new() { GuildBranchId = GuildBranchId, Character = new Character { UserDiscordId = playerDiscordId } };

    [Fact]
    public async Task BuildAsync_NoRosterMembers_ReturnsEmptyList()
    {
        var result = await _sut.BuildAsync(new RaidEvent { Id = EventId, GuildBranchId = GuildBranchId });

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildAsync_MemberWithNoResponse_ListedWithNullFields()
    {
        _guildMembershipRepository.Setup(m => m.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync([MakeMembership("player-1")]);
        _usersRepository.Setup(u => u.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), default))
            .ReturnsAsync([new User { DiscordId = "player-1", Name = "Thrall" }]);

        var result = await _sut.BuildAsync(new RaidEvent { Id = EventId, GuildBranchId = GuildBranchId });

        result.Should().ContainSingle(r => r.UserDiscordId == "player-1" && r.PlayerName == "Thrall" && r.Status == null && r.CharacterId == null);
    }

    [Fact]
    public async Task BuildAsync_MemberWithAcceptedResponse_MapsCharacterClassAndSpecFields()
    {
        _guildMembershipRepository.Setup(m => m.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync([MakeMembership("player-1")]);
        _usersRepository.Setup(u => u.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), default))
            .ReturnsAsync([new User { DiscordId = "player-1", Name = "Thrall" }]);
        _raidSignupRepository.Setup(r => r.GetForEventAsync(EventId, default)).ReturnsAsync(
        [
            new RaidSignup
            {
                RaidEventId = EventId,
                UserDiscordId = "player-1",
                Status = SignupStatus.Accepted,
                CharacterId = 42,
                SpecId = 71,
                Character = new Character { Id = 42, Name = "Arthas", ClassId = 1, Class = new WowClass { Id = 1, Name = "Warrior", Color = "C79C6E" } },
                Spec = new Spec { Id = 71, Name = "Arms", IconUrl = "arms.png" },
            },
        ]);

        var result = await _sut.BuildAsync(new RaidEvent { Id = EventId, GuildBranchId = GuildBranchId });

        var response = result.Should().ContainSingle().Which;
        response.Status.Should().Be(SignupStatus.Accepted);
        response.CharacterId.Should().Be(42);
        response.CharacterName.Should().Be("Arthas");
        response.ClassId.Should().Be(1);
        response.ClassName.Should().Be("Warrior");
        response.SpecId.Should().Be(71);
        response.SpecName.Should().Be("Arms");
        response.SpecIconUrl.Should().Be("arms.png");
    }

    [Fact]
    public async Task BuildAsync_DuplicateGuildBranchIdsAcrossMemberships_DeduplicatesPlayers()
    {
        _guildMembershipRepository.Setup(m => m.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync([MakeMembership("player-1"), MakeMembership("player-1")]);
        _usersRepository.Setup(u => u.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), default))
            .ReturnsAsync([new User { DiscordId = "player-1", Name = "Thrall" }]);

        var result = await _sut.BuildAsync(new RaidEvent { Id = EventId, GuildBranchId = GuildBranchId });

        result.Should().ContainSingle();
    }
}
