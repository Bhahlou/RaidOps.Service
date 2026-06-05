using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Characters.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Implementations.Characters.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Characters.CommandHandlers;

public class ImportCharactersCommandHandlerTests
{
    private readonly Mock<IBnetAccountRepository> _bnetAccounts  = new();
    private readonly Mock<IBranchRepository>      _branches      = new();
    private readonly Mock<IRealmRepository>        _realms        = new();
    private readonly Mock<ICharacterRepository>   _characters    = new();
    private readonly ImportCharactersCommandHandler _sut;

    private const string DiscordId = "user-1";
    private const int    BranchId  = 1;

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
        Id                   = BranchId,
        Name                 = "Retail",
        BnetNamespacePrefix  = "dynamic",
        CurrentExpansionId   = 10,
    };

    private static readonly Realm ExistingRealm = new()
    {
        Id       = 7,
        Slug     = "kazzak",
        Name     = "Kazzak",
        Region   = "eu",
        BranchId = BranchId,
    };

    public ImportCharactersCommandHandlerTests()
    {
        _sut = new ImportCharactersCommandHandler(
            _bnetAccounts.Object,
            _branches.Object,
            _realms.Object,
            _characters.Object);

        // Default: upsert returns the character as-is
        _characters.Setup(r => r.UpsertAsync(It.IsAny<Character>(), default))
            .ReturnsAsync((Character c, CancellationToken _) => c);
    }

    // ── Guard clauses ────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_NoBnetAccount_ReturnsBnetNotLinked()
    {
        _bnetAccounts.Setup(r => r.GetByDiscordIdAsync(DiscordId, default))
            .ReturnsAsync((BattleNetAccount?)null);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.BnetNotLinked);
    }

    [Fact]
    public async Task HandleAsync_BranchNotFound_ReturnsBranchNotFound()
    {
        _bnetAccounts.Setup(r => r.GetByDiscordIdAsync(DiscordId, default)).ReturnsAsync(Account);
        _branches.Setup(r => r.GetByIdAsync(BranchId, default)).ReturnsAsync((Branch?)null);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.BranchNotFound);
    }

    // ── Realm resolution ─────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_ExistingRealm_SkipsRealmCreation()
    {
        ArrangeHappyPath();
        _realms.Setup(r => r.GetBySlugAndBranchAsync("kazzak", BranchId, default))
            .ReturnsAsync(ExistingRealm);

        await _sut.HandleAsync(MakeCommand(OneCharacter("kazzak")));

        _realms.Verify(r => r.AddAsync(It.IsAny<Realm>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_NewRealm_CreatesRealmAndImportsCharacter()
    {
        ArrangeHappyPath();
        _realms.Setup(r => r.GetBySlugAndBranchAsync("kazzak", BranchId, default))
            .ReturnsAsync((Realm?)null);
        _realms.Setup(r => r.AddAsync(It.IsAny<Realm>(), default))
            .ReturnsAsync((Realm realm, CancellationToken _) => realm);

        var result = await _sut.HandleAsync(MakeCommand(OneCharacter("kazzak")));

        result.IsSuccess.Should().BeTrue();
        _realms.Verify(r => r.AddAsync(
            It.Is<Realm>(realm => realm.Slug == "kazzak" && realm.Region == "eu"),
            default), Times.Once);
    }

    // ── Import count ─────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_MultipleCharacters_ReturnsImportCount()
    {
        ArrangeHappyPath();
        _realms.Setup(r => r.GetBySlugAndBranchAsync(It.IsAny<string>(), BranchId, default))
            .ReturnsAsync(ExistingRealm);

        var command = MakeCommand(
            OneCharacter("kazzak", bnetId: 1),
            OneCharacter("kazzak", bnetId: 2),
            OneCharacter("silvermoon", bnetId: 3));

        var result = await _sut.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Message.Should().StartWith("3");
        _characters.Verify(r => r.UpsertExpansionStateAsync(It.IsAny<CharacterExpansionState>(), default), Times.Exactly(3));
    }

    // ── Faction mapping ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("ALLIANCE", Faction.Alliance)]
    [InlineData("alliance", Faction.Alliance)]
    [InlineData("HORDE",    Faction.Horde)]
    [InlineData("NEUTRAL",  Faction.Neutral)]
    [InlineData("UNKNOWN",  Faction.Neutral)]
    public async Task HandleAsync_FactionString_MapsCorrectly(string factionString, Faction expectedFaction)
    {
        ArrangeHappyPath();
        _realms.Setup(r => r.GetBySlugAndBranchAsync("kazzak", BranchId, default))
            .ReturnsAsync(ExistingRealm);

        await _sut.HandleAsync(MakeCommand(OneCharacter("kazzak", faction: factionString)));

        _characters.Verify(r => r.UpsertAsync(
            It.Is<Character>(c => c.Faction == expectedFaction),
            default), Times.Once);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void ArrangeHappyPath()
    {
        _bnetAccounts.Setup(r => r.GetByDiscordIdAsync(DiscordId, default)).ReturnsAsync(Account);
        _branches.Setup(r => r.GetByIdAsync(BranchId, default)).ReturnsAsync(Branch);
    }

    private static ImportCharactersCommand MakeCommand(params CharacterToImportDto[] characters) => new()
    {
        UserDiscordId = DiscordId,
        BranchId      = BranchId,
        Characters    = characters.Length > 0 ? characters : [OneCharacter("kazzak")],
    };

    private static CharacterToImportDto OneCharacter(
        string realmSlug,
        long   bnetId  = 1001,
        string faction = "ALLIANCE") => new()
    {
        BnetCharacterId = bnetId,
        Name            = "Arthas",
        RealmSlug       = realmSlug,
        RealmName       = realmSlug,
        ClassId         = 1,
        RaceId          = 1,
        Faction         = faction,
        Level           = 80,
    };
}
