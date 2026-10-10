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
    private readonly Mock<ICharacterRepository>   _characters   = new();
    private readonly Mock<IBnetAccountRepository> _bnetAccounts = new();
    private readonly GetCharactersQueryHandler    _sut;

    private const string DiscordId = "user-1";

    private static readonly GetCharactersQuery Query = new() { UserDiscordId = DiscordId };

    public GetCharactersQueryHandlerTests()
    {
        _sut = new GetCharactersQueryHandler(_characters.Object, _bnetAccounts.Object);
        _bnetAccounts.Setup(r => r.GetAllByDiscordIdAsync(DiscordId, default)).ReturnsAsync((IReadOnlyList<BattleNetAccount>)[]);
    }

    [Fact]
    public async Task HandleAsync_ReturnsActiveDtosForUser()
    {
        _characters.Setup(r => r.GetByUserWithDetailsAsync(DiscordId, true, default))
            .ReturnsAsync([MakeCharacter(1, "Arthas"), MakeCharacter(2, "Sylvanas")]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Characters.Should().HaveCount(2);
        result.Value.Characters.Should().ContainSingle(d => d.Name == "Arthas");
        result.Value.Characters.Should().ContainSingle(d => d.Name == "Sylvanas");
    }

    [Fact]
    public async Task HandleAsync_EmptyList_ReturnsOkWithEmptyCollection()
    {
        _characters.Setup(r => r.GetByUserWithDetailsAsync(DiscordId, true, default))
            .ReturnsAsync([]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Characters.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_QueriesActiveOnly()
    {
        _characters.Setup(r => r.GetByUserWithDetailsAsync(DiscordId, true, default))
            .ReturnsAsync([]);

        await _sut.HandleAsync(Query, default);

        _characters.Verify(r => r.GetByUserWithDetailsAsync(DiscordId, true, default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_NoBnetAccountLinked_ReturnsEmptyBnetAccounts()
    {
        _characters.Setup(r => r.GetByUserWithDetailsAsync(DiscordId, true, default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(Query, default);

        result.Value!.BnetAccounts.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_OneBnetAccountLinked_ReturnsMappedBnetAccount()
    {
        var expiry = DateTimeOffset.UtcNow.AddHours(1);
        _characters.Setup(r => r.GetByUserWithDetailsAsync(DiscordId, true, default)).ReturnsAsync([]);
        _bnetAccounts.Setup(r => r.GetAllByDiscordIdAsync(DiscordId, default))
            .ReturnsAsync((IReadOnlyList<BattleNetAccount>)
            [
                new BattleNetAccount
                {
                    UserDiscordId = DiscordId,
                    BnetId        = "bnet-42",
                    BattleTag     = "Player#1234",
                    AccessToken   = "tok",
                    Region        = "eu",
                    TokenExpiry   = expiry,
                },
            ]);

        var result = await _sut.HandleAsync(Query, default);

        result.Value!.BnetAccounts.Should().ContainSingle();
        result.Value.BnetAccounts[0].BnetId.Should().Be("bnet-42");
        result.Value.BnetAccounts[0].BattleTag.Should().Be("Player#1234");
        result.Value.BnetAccounts[0].Region.Should().Be("eu");
        result.Value.BnetAccounts[0].TokenExpiry.Should().Be(expiry);
    }

    [Fact]
    public async Task HandleAsync_MultipleBnetAccountsLinked_ReturnsAllOfThem()
    {
        _characters.Setup(r => r.GetByUserWithDetailsAsync(DiscordId, true, default)).ReturnsAsync([]);
        _bnetAccounts.Setup(r => r.GetAllByDiscordIdAsync(DiscordId, default))
            .ReturnsAsync((IReadOnlyList<BattleNetAccount>)
            [
                new BattleNetAccount { UserDiscordId = DiscordId, BnetId = "bnet-1", BattleTag = "Player#1234", AccessToken = "tok-1", Region = "eu" },
                new BattleNetAccount { UserDiscordId = DiscordId, BnetId = "bnet-2", BattleTag = "Player#5678", AccessToken = "tok-2", Region = "us" },
            ]);

        var result = await _sut.HandleAsync(Query, default);

        result.Value!.BnetAccounts.Should().HaveCount(2);
        result.Value.BnetAccounts.Select(a => a.BnetId).Should().BeEquivalentTo(["bnet-1", "bnet-2"]);
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
        GuildMemberships = [],
    };
}
