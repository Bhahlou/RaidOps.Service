using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Signups.Queries;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Signups.QueryHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Signups.QueryHandlers;

public class GetMyRosterCharactersQueryHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IGuildMembershipRepository> _guildMembershipRepository = new();
    private readonly Mock<ICharacterRepository> _characterRepository = new();
    private readonly GetMyRosterCharactersQueryHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "player-1";

    private static readonly GetMyRosterCharactersQuery Query = new() { GuildId = GuildId, RequesterDiscordId = RequesterId, GuildBranchId = GuildBranchId };

    public GetMyRosterCharactersQueryHandlerTests()
    {
        _sut = new GetMyRosterCharactersQueryHandler(_access.Object, _guildMembershipRepository.Object, _characterRepository.Object);
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _guildMembershipRepository.Setup(m => m.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync([]);
        _characterRepository.Setup(c => c.GetRaidSpecsForCharactersAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([]);
    }

    private static Character MakeCharacter(int id, string name, string userDiscordId = RequesterId, int classId = 1) => new()
    {
        Id = id,
        Name = name,
        UserDiscordId = userDiscordId,
        ClassId = classId,
        Branch = new Branch { Name = "Classic Anniversary" },
        Realm = new Realm { Slug = "gehennas" },
    };

    private static GuildMembership MakeMembership(Character character) => new() { GuildBranchId = GuildBranchId, CharacterId = character.Id, Character = character };

    private static CharacterRaidSpec MakeRaidSpec(int characterId, int specId, string specName, bool isMain) =>
        new() { CharacterId = characterId, SpecId = specId, IsMain = isMain, Spec = new Spec { Id = specId, Name = specName } };

    [Fact]
    public async Task HandleAsync_BelowRosterAccess_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Public);

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
        _guildMembershipRepository.Verify(m => m.GetByGuildBranchIdAsync(It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_RosterAccess_Succeeds()
    {
        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_NoMemberships_ReturnsEmptyList()
    {
        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_ExcludesCharactersBelongingToOtherPlayers()
    {
        _guildMembershipRepository.Setup(m => m.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync(
        [
            MakeMembership(MakeCharacter(1, "Addse", RequesterId)),
            MakeMembership(MakeCharacter(2, "Jaina", "player-2")),
        ]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(c => c.CharacterId == 1);
    }

    [Fact]
    public async Task HandleAsync_MapsCharacterFields()
    {
        _guildMembershipRepository.Setup(m => m.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync(
        [
            MakeMembership(MakeCharacter(1, "Addse", RequesterId, classId: 6)),
        ]);

        var result = await _sut.HandleAsync(Query, default);

        var character = result.Value.Should().ContainSingle().Which;
        character.CharacterId.Should().Be(1);
        character.CharacterName.Should().Be("Addse");
        character.ClassId.Should().Be(6);
        character.BranchName.Should().Be("Classic Anniversary");
        character.RealmSlug.Should().Be("gehennas");
    }

    [Fact]
    public async Task HandleAsync_CharacterWithNoRaidSpecs_ReturnsEmptyRaidSpecsList()
    {
        _guildMembershipRepository.Setup(m => m.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync([MakeMembership(MakeCharacter(1, "Addse"))]);

        var result = await _sut.HandleAsync(Query, default);

        result.Value.Should().ContainSingle().Which.RaidSpecs.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_MapsRaidSpecsWithMainSpecFirst()
    {
        _guildMembershipRepository.Setup(m => m.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync([MakeMembership(MakeCharacter(1, "Addse"))]);
        _characterRepository.Setup(c => c.GetRaidSpecsForCharactersAsync(It.Is<IEnumerable<int>>(ids => ids.Single() == 1), default)).ReturnsAsync(
        [
            MakeRaidSpec(1, 71, "Arms", isMain: false),
            MakeRaidSpec(1, 72, "Fury", isMain: true),
        ]);

        var result = await _sut.HandleAsync(Query, default);

        var raidSpecs = result.Value.Should().ContainSingle().Which.RaidSpecs;
        raidSpecs.Select(s => s.SpecName).Should().Equal("Fury", "Arms");
        raidSpecs.Should().Contain(s => s.SpecId == 72 && s.IsMain);
    }

    [Fact]
    public async Task HandleAsync_MultipleCharacters_SortedByNameCaseInsensitive()
    {
        _guildMembershipRepository.Setup(m => m.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync(
        [
            MakeMembership(MakeCharacter(1, "zeta")),
            MakeMembership(MakeCharacter(2, "Alpha")),
        ]);

        var result = await _sut.HandleAsync(Query, default);

        result.Value!.Select(c => c.CharacterName).Should().Equal("Alpha", "zeta");
    }
}
