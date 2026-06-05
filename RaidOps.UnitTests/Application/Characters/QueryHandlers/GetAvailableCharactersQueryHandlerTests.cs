using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Characters.Queries;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Implementations.Characters.QueryHandlers;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Reference;
using RaidOps.ExternalApplication.Contracts.Services.BNet;
using RaidOps.ExternalApplication.Contracts.Services.BNet.Responses;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Characters.QueryHandlers;

public class GetAvailableCharactersQueryHandlerTests
{
    private readonly Mock<IBnetAccountRepository>              _bnetAccounts = new();
    private readonly Mock<IBranchRepository>                   _branches     = new();
    private readonly Mock<ICharacterRepository>                _characters   = new();
    private readonly Mock<IBnetApiService>                     _bnetApi      = new();
    private readonly GetAvailableCharactersQueryHandler        _sut;

    private const string DiscordId = "user-1";
    private const int    BranchId  = 1;

    private static readonly GetAvailableCharactersQuery Query = new()
    {
        UserDiscordId = DiscordId,
        BranchId      = BranchId,
    };

    private static readonly BattleNetAccount Account = new()
    {
        UserDiscordId = DiscordId,
        BnetId        = "bnet-1",
        BattleTag     = "Player#1234",
        AccessToken   = "tok",
        Region        = "eu",
    };

    private static readonly Branch Branch = new()
    {
        Id                  = BranchId,
        Name                = "Retail",
        BnetNamespacePrefix = "dynamic",
        CurrentExpansionId  = 10,
    };

    public GetAvailableCharactersQueryHandlerTests()
    {
        _sut = new GetAvailableCharactersQueryHandler(
            _bnetAccounts.Object,
            _branches.Object,
            _characters.Object,
            _bnetApi.Object);
    }

    // ── Guard clauses ────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_NoBnetAccount_ReturnsBnetNotLinked()
    {
        _bnetAccounts.Setup(r => r.GetByDiscordIdAsync(DiscordId, default))
            .ReturnsAsync((BattleNetAccount?)null);

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.BnetNotLinked);
    }

    [Fact]
    public async Task HandleAsync_BranchNotFound_ReturnsBranchNotFound()
    {
        _bnetAccounts.Setup(r => r.GetByDiscordIdAsync(DiscordId, default)).ReturnsAsync(Account);
        _branches.Setup(r => r.GetByIdAsync(BranchId, default)).ReturnsAsync((Branch?)null);

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.BranchNotFound);
    }

    // ── Happy path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_ReturnsCharactersWithAlreadyImportedFlag()
    {
        ArrangeHappyPath(
            bnetCharacters: [BnetChar(id: 101, name: "Arthas"), BnetChar(id: 202, name: "Sylvanas")],
            importedIds: [101]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Single(c => c.Name == "Arthas").AlreadyImported.Should().BeTrue();
        result.Value.Single(c => c.Name == "Sylvanas").AlreadyImported.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_CharactersOrderedByLevelDescThenByName()
    {
        ArrangeHappyPath(
            bnetCharacters:
            [
                BnetChar(id: 1, name: "Zzz",   level: 60),
                BnetChar(id: 2, name: "Aaa",   level: 80),
                BnetChar(id: 3, name: "Mmm",   level: 80),
            ],
            importedIds: []);

        var result = await _sut.HandleAsync(Query, default);

        var names = result.Value!.Select(c => c.Name).ToList();
        names.Should().ContainInOrder("Aaa", "Mmm", "Zzz");
    }

    [Fact]
    public async Task HandleAsync_BuildsCorrectProfileNamespaceForBranch()
    {
        // "dynamic" → "profile-eu"
        _bnetAccounts.Setup(r => r.GetByDiscordIdAsync(DiscordId, default)).ReturnsAsync(Account);
        _branches.Setup(r => r.GetByIdAsync(BranchId, default))
            .ReturnsAsync(new Branch { Id = BranchId, Name = "Retail", BnetNamespacePrefix = "dynamic", CurrentExpansionId = 10 });
        _characters.Setup(r => r.GetBnetIdsByUserAsync(DiscordId, default)).ReturnsAsync([]);
        _bnetApi.Setup(r => r.GetWowCharactersAsync("tok", "eu", It.IsAny<string>(), default))
            .ReturnsAsync(new BnetWowAccountsResponse { WowAccounts = [] });

        await _sut.HandleAsync(Query, default);

        _bnetApi.Verify(r => r.GetWowCharactersAsync("tok", "eu", "profile-eu", default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ClassicBranch_BuildsCorrectProfileNamespace()
    {
        // "dynamic-classic1x" → "profile-classic1x-eu"
        var classicBranch = new Branch { Id = BranchId, Name = "Classic Era", BnetNamespacePrefix = "dynamic-classic1x", CurrentExpansionId = 2 };
        _bnetAccounts.Setup(r => r.GetByDiscordIdAsync(DiscordId, default)).ReturnsAsync(Account);
        _branches.Setup(r => r.GetByIdAsync(BranchId, default)).ReturnsAsync(classicBranch);
        _characters.Setup(r => r.GetBnetIdsByUserAsync(DiscordId, default)).ReturnsAsync([]);
        _bnetApi.Setup(r => r.GetWowCharactersAsync("tok", "eu", It.IsAny<string>(), default))
            .ReturnsAsync(new BnetWowAccountsResponse { WowAccounts = [] });

        await _sut.HandleAsync(Query, default);

        _bnetApi.Verify(r => r.GetWowCharactersAsync("tok", "eu", "profile-classic1x-eu", default), Times.Once);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void ArrangeHappyPath(
        IEnumerable<BnetWowCharacterDto> bnetCharacters,
        IEnumerable<long>                importedIds)
    {
        _bnetAccounts.Setup(r => r.GetByDiscordIdAsync(DiscordId, default)).ReturnsAsync(Account);
        _branches.Setup(r => r.GetByIdAsync(BranchId, default)).ReturnsAsync(Branch);
        _characters.Setup(r => r.GetBnetIdsByUserAsync(DiscordId, default))
            .ReturnsAsync([.. importedIds]);
        _bnetApi.Setup(r => r.GetWowCharactersAsync(Account.AccessToken, Account.Region, It.IsAny<string>(), default))
            .ReturnsAsync(new BnetWowAccountsResponse
            {
                WowAccounts = [new BnetWowAccountDto { Id = 1, Characters = [.. bnetCharacters] }],
            });
    }

    private static BnetWowCharacterDto BnetChar(long id, string name, int level = 80) => new()
    {
        Id   = id,
        Name = name,
        Level = level,
        Realm         = new BnetRealmRefDto { Slug = "kazzak", Name = "Kazzak" },
        PlayableClass = new BnetIdRefDto    { Id = 1, Name = "Warrior" },
        PlayableRace  = new BnetIdRefDto    { Id = 1, Name = "Human" },
        Gender        = new BnetTypeRefDto  { Type = "MALE" },
        Faction       = new BnetTypeRefDto  { Type = "ALLIANCE" },
    };
}
