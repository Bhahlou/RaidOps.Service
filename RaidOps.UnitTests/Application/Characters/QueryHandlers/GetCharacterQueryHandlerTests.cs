using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Characters.Queries;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Characters.QueryHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Characters.QueryHandlers;

/// <summary>
/// Unit tests for <see cref="GetCharacterQueryHandler"/>.
/// </summary>
public class GetCharacterQueryHandlerTests
{
    private readonly Mock<IBranchRepository>          _branches    = new();
    private readonly Mock<ICharacterRepository>       _characters  = new();
    private readonly Mock<IGuildMembershipRepository> _memberships = new();
    private readonly Mock<IGuildAccessService>        _guildAccess = new();
    private readonly GetCharacterQueryHandler         _sut;

    private const string OwnerId    = "owner-1";
    private const string GuildId    = "guild-1";
    private const int    BranchId   = 1;

    private static GetCharacterQuery MakeQuery(string requesterDiscordId = OwnerId, string branchSlug = "classic-anniversary") => new()
    {
        BranchSlug = branchSlug,
        RealmSlug = "kazzak",
        CharacterName = "arthas",
        RequesterDiscordId = requesterDiscordId,
    };

    public GetCharacterQueryHandlerTests()
    {
        _sut = new GetCharacterQueryHandler(_branches.Object, _characters.Object, _memberships.Object, _guildAccess.Object);
        _branches.Setup(b => b.GetAllAsync(default))
            .ReturnsAsync([new Branch { Id = BranchId, Name = "Classic Anniversary" }]);
    }

    [Fact]
    public async Task HandleAsync_UnknownBranchSlug_ReturnsNotFound()
    {
        var query = MakeQuery(branchSlug: "retail");

        var result = await _sut.HandleAsync(query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.NotFound);
        _characters.Verify(r => r.GetByBranchRealmAndNameAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_CharacterNotFound_ReturnsNotFound()
    {
        _characters.Setup(r => r.GetByBranchRealmAndNameAsync(BranchId, "kazzak", "arthas", default))
            .ReturnsAsync((Character?)null);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.NotFound);
    }

    [Fact]
    public async Task HandleAsync_Owner_ReturnsIsOwnerAndCanEditRaidSpecsTrue()
    {
        _characters.Setup(r => r.GetByBranchRealmAndNameAsync(BranchId, "kazzak", "arthas", default))
            .ReturnsAsync(BuildCharacter());

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsOwner.Should().BeTrue();
        result.Value!.CanEditRaidSpecs.Should().BeTrue();
        _memberships.Verify(r => r.GetByCharacterIdAsync(It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_NotOwnerNoSharedGuild_ReturnsNotFound()
    {
        _characters.Setup(r => r.GetByBranchRealmAndNameAsync(BranchId, "kazzak", "arthas", default))
            .ReturnsAsync(BuildCharacter());
        _memberships.Setup(r => r.GetByCharacterIdAsync(1, default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(MakeQuery(requesterDiscordId: "stranger"), default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.NotFound);
    }

    [Fact]
    public async Task HandleAsync_NotOwnerWithRosterAccess_ReturnsViewOnly()
    {
        _characters.Setup(r => r.GetByBranchRealmAndNameAsync(BranchId, "kazzak", "arthas", default))
            .ReturnsAsync(BuildCharacter());
        _memberships.Setup(r => r.GetByCharacterIdAsync(1, default))
            .ReturnsAsync([new GuildMembership { CharacterId = 1, GuildId = GuildId }]);
        _guildAccess.Setup(a => a.GetAccessLevelAsync("teammate", GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(MakeQuery(requesterDiscordId: "teammate"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsOwner.Should().BeFalse();
        result.Value!.CanEditRaidSpecs.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_NotOwnerOfficer_ReturnsCanEditRaidSpecsTrue()
    {
        _characters.Setup(r => r.GetByBranchRealmAndNameAsync(BranchId, "kazzak", "arthas", default))
            .ReturnsAsync(BuildCharacter());
        _memberships.Setup(r => r.GetByCharacterIdAsync(1, default))
            .ReturnsAsync([new GuildMembership { CharacterId = 1, GuildId = GuildId }]);
        _guildAccess.Setup(a => a.GetAccessLevelAsync("officer", GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);

        var result = await _sut.HandleAsync(MakeQuery(requesterDiscordId: "officer"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsOwner.Should().BeFalse();
        result.Value!.CanEditRaidSpecs.Should().BeTrue();
    }

    private static Character BuildCharacter() => new()
    {
        Id            = 1,
        Name          = "Arthas",
        UserDiscordId = OwnerId,
        ClassId       = 6,
        Class         = new WowClass { Id = 6, Name = "Death Knight", Color = "C41F3B" },
        RaceId        = 1,
        Race          = new Race { Id = 1, Name = "Human" },
        BranchId      = BranchId,
        Branch        = new Branch { Id = BranchId, Name = "Classic Anniversary" },
        RealmId       = 1,
        Realm         = new Realm { Id = 1, Name = "Kazzak", Slug = "kazzak", Region = "eu" },
        RaidSpecs     = [],
        ExpansionStates = [],
        GuildMemberships = [],
    };
}
