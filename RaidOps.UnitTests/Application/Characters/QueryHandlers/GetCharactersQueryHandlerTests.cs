using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Characters.Queries;
using RaidOps.Application.Implementations.Characters.QueryHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Characters.QueryHandlers;

public class GetCharactersQueryHandlerTests
{
    private readonly Mock<ICharacterRepository> _characters = new();
    private readonly GetCharactersQueryHandler  _sut;

    private const string DiscordId = "user-1";

    private static readonly GetCharactersQuery Query = new() { UserDiscordId = DiscordId };

    public GetCharactersQueryHandlerTests()
    {
        _sut = new GetCharactersQueryHandler(_characters.Object);
    }

    [Fact]
    public async Task HandleAsync_ReturnsActiveDtosForUser()
    {
        _characters.Setup(r => r.GetByUserWithDetailsAsync(DiscordId, true, default))
            .ReturnsAsync([MakeCharacter(1, "Arthas"), MakeCharacter(2, "Sylvanas")]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().ContainSingle(d => d.Name == "Arthas");
        result.Value.Should().ContainSingle(d => d.Name == "Sylvanas");
    }

    [Fact]
    public async Task HandleAsync_EmptyList_ReturnsOkWithEmptyCollection()
    {
        _characters.Setup(r => r.GetByUserWithDetailsAsync(DiscordId, true, default))
            .ReturnsAsync([]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_QueriesActiveOnly()
    {
        _characters.Setup(r => r.GetByUserWithDetailsAsync(DiscordId, true, default))
            .ReturnsAsync([]);

        await _sut.HandleAsync(Query, default);

        _characters.Verify(r => r.GetByUserWithDetailsAsync(DiscordId, true, default), Times.Once);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Character MakeCharacter(int id, string name) => new()
    {
        Id            = id,
        Name          = name,
        Faction       = Faction.Alliance,
        UserDiscordId = DiscordId,
        Class  = new WowClass { Id = 1, Name = "Warrior", Color = "C69B3A" },
        Race   = new Race { Id = 1, Name = "Human" },
        Branch = new Branch { Id = 1, Name = "Retail",  BnetNamespacePrefix = "dynamic", CurrentExpansionId = 10 },
        Realm  = new Realm  { Id = 1, Name = "Kazzak", Slug = "kazzak", Region = "eu", BranchId = 1 },
        ExpansionStates = [],
    };
}
