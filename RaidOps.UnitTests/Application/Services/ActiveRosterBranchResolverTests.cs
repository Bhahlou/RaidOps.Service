using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Services;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Discord;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Services;

public class ActiveRosterBranchResolverTests
{
    private readonly Mock<ICharacterRepository> _characters = new();
    private readonly Mock<IGuildMembershipRepository> _memberships = new();
    private readonly ActiveRosterBranchResolver _sut;

    private const string RequesterId = "user-1";

    private static readonly int[] TwoCharacterIds = [1, 2];
    private static readonly int[] ThreeCharacterIds = [1, 2, 3];

    public ActiveRosterBranchResolverTests()
    {
        _sut = new ActiveRosterBranchResolver(_characters.Object, _memberships.Object);
    }

    [Fact]
    public async Task GetActiveBranchesAsync_NoActiveCharacters_ReturnsEmptyAndNeverQueriesMemberships()
    {
        _characters.Setup(c => c.GetByUserWithDetailsAsync(RequesterId, true, default)).ReturnsAsync([]);

        var result = await _sut.GetActiveBranchesAsync(RequesterId);

        result.Should().BeEmpty();
        _memberships.Verify(m => m.GetByCharacterIdsAsync(It.IsAny<IEnumerable<int>>(), default), Times.Never);
    }

    [Fact]
    public async Task GetActiveBranchesAsync_MultipleActiveCharactersOnSameBranch_DedupesToOneEntry()
    {
        _characters.Setup(c => c.GetByUserWithDetailsAsync(RequesterId, true, default))
            .ReturnsAsync([new Character { Id = 1 }, new Character { Id = 2 }]);
        _memberships.Setup(m => m.GetByCharacterIdsAsync(It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(TwoCharacterIds)), default))
            .ReturnsAsync([
                new GuildMembership { CharacterId = 1, GuildId = "guild-1", GuildBranchId = 10 },
                new GuildMembership { CharacterId = 2, GuildId = "guild-1", GuildBranchId = 10 },
            ]);

        var result = await _sut.GetActiveBranchesAsync(RequesterId);

        result.Should().ContainSingle();
        result[0].GuildId.Should().Be("guild-1");
        result[0].GuildBranchId.Should().Be(10);
    }

    [Fact]
    public async Task GetActiveBranchesAsync_ActiveCharactersAcrossMultipleGuildsAndBranches_ReturnsOneEntryPerDistinctPair()
    {
        _characters.Setup(c => c.GetByUserWithDetailsAsync(RequesterId, true, default))
            .ReturnsAsync([new Character { Id = 1 }, new Character { Id = 2 }, new Character { Id = 3 }]);
        _memberships.Setup(m => m.GetByCharacterIdsAsync(It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(ThreeCharacterIds)), default))
            .ReturnsAsync([
                new GuildMembership { CharacterId = 1, GuildId = "guild-1", GuildBranchId = 10 },
                new GuildMembership { CharacterId = 2, GuildId = "guild-1", GuildBranchId = 20 },
                new GuildMembership { CharacterId = 3, GuildId = "guild-2", GuildBranchId = 30 },
            ]);

        var result = await _sut.GetActiveBranchesAsync(RequesterId);

        result.Should().BeEquivalentTo(
        [
            new ActiveRosterBranch("guild-1", 10),
            new ActiveRosterBranch("guild-1", 20),
            new ActiveRosterBranch("guild-2", 30),
        ]);
    }
}
