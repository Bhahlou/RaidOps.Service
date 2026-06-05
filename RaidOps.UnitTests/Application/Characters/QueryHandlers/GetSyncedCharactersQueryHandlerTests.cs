using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Characters.Queries;
using RaidOps.Application.Implementations.Characters.QueryHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Characters.QueryHandlers;

public class GetSyncedCharactersQueryHandlerTests
{
    private readonly Mock<ICharacterRepository>     _characters = new();
    private readonly GetSyncedCharactersQueryHandler _sut;

    private const string DiscordId = "user-1";

    private static readonly GetSyncedCharactersQuery Query = new() { UserDiscordId = DiscordId };

    public GetSyncedCharactersQueryHandlerTests()
    {
        _sut = new GetSyncedCharactersQueryHandler(_characters.Object);
    }

    [Fact]
    public async Task HandleAsync_QueriesAllCharacters_NotActiveOnly()
    {
        _characters.Setup(r => r.GetByUserWithDetailsAsync(DiscordId, false, default)).ReturnsAsync([]);

        await _sut.HandleAsync(Query, default);

        _characters.Verify(r => r.GetByUserWithDetailsAsync(DiscordId, false, default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ActiveExpansionState_UsesActiveLevel()
    {
        var character = MakeCharacter(activeLevel: 80, inactiveLevel: 60, isActive: true);
        _characters.Setup(r => r.GetByUserWithDetailsAsync(DiscordId, false, default))
            .ReturnsAsync([character]);

        var result = await _sut.HandleAsync(Query, default);

        result.Value!.Single().Level.Should().Be(80);
    }

    [Fact]
    public async Task HandleAsync_NoActiveState_FallsBackToHighestLevel()
    {
        var character = MakeCharacter(activeLevel: 60, inactiveLevel: 80, isActive: false);
        _characters.Setup(r => r.GetByUserWithDetailsAsync(DiscordId, false, default))
            .ReturnsAsync([character]);

        var result = await _sut.HandleAsync(Query, default);

        result.Value!.Single().Level.Should().Be(80);
    }

    [Fact]
    public async Task HandleAsync_NoExpansionStates_ReturnsLevelZero()
    {
        var character = MakeCharacter();
        character.ExpansionStates = [];
        _characters.Setup(r => r.GetByUserWithDetailsAsync(DiscordId, false, default))
            .ReturnsAsync([character]);

        var result = await _sut.HandleAsync(Query, default);

        result.Value!.Single().Level.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_MapsIsActiveFlag()
    {
        var character = MakeCharacter();
        character.IsActiveInRaidOps = true;
        _characters.Setup(r => r.GetByUserWithDetailsAsync(DiscordId, false, default))
            .ReturnsAsync([character]);

        var result = await _sut.HandleAsync(Query, default);

        result.Value!.Single().IsActive.Should().BeTrue();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Character MakeCharacter(int activeLevel = 80, int inactiveLevel = 60, bool isActive = true) => new()
    {
        Id            = 1,
        Name          = "Arthas",
        Faction       = Faction.Alliance,
        UserDiscordId = DiscordId,
        Class  = new WowClass { Id = 1, Name = "Death Knight", Color = "C41F3B" },
        Race   = new Race { Id = 1, Name = "Human" },
        Branch = new Branch { Id = 1, Name = "Retail", BnetNamespacePrefix = "dynamic", CurrentExpansionId = 10 },
        Realm  = new Realm  { Id = 1, Name = "Kazzak", Slug = "kazzak", Region = "eu", BranchId = 1 },
        ExpansionStates =
        [
            new CharacterExpansionState { ExpansionId = 10, Level = activeLevel,   IsActive = isActive },
            new CharacterExpansionState { ExpansionId = 9,  Level = inactiveLevel, IsActive = false },
        ],
    };
}
