using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RaidOps.Application.Contracts.Characters.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Implementations.Characters.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Reference;
using RaidOps.ExternalApplication.Contracts.Services.BNet;
using RaidOps.ExternalApplication.Contracts.Services.BNet.Responses;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Characters.CommandHandlers;

public class SyncBnetCharactersCommandHandlerTests
{
    private readonly Mock<IBnetAccountRepository> _bnetAccounts = new();
    private readonly Mock<IBranchRepository>      _branches     = new();
    private readonly Mock<IRealmRepository>       _realms       = new();
    private readonly Mock<ICharacterRepository>   _characters   = new();
    private readonly Mock<IBnetApiService>        _bnetApi      = new();
    private readonly SyncBnetCharactersCommandHandler _sut;

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

    private static readonly BattleNetAccount SecondAccount = new()
    {
        UserDiscordId = DiscordId,
        BnetId        = "bnet-2",
        BattleTag     = "Player#5678",
        AccessToken   = "tok-2",
        Region        = "us",
    };

    private static readonly Branch Branch = new()
    {
        Id                  = BranchId,
        Name                = "Retail",
        BnetNamespacePrefix = "dynamic",
        CurrentExpansionId  = 10,
    };

    private static readonly Realm ExistingRealm = new()
    {
        Id       = 7,
        Slug     = "kazzak",
        Name     = "Kazzak",
        Region   = "eu",
        BranchId = BranchId,
    };

    private static readonly SyncBnetCharactersCommand Command = new()
    {
        UserDiscordId = DiscordId,
        BranchId      = BranchId,
    };

    public SyncBnetCharactersCommandHandlerTests()
    {
        _sut = new SyncBnetCharactersCommandHandler(
            _bnetAccounts.Object,
            _branches.Object,
            _realms.Object,
            _characters.Object,
            _bnetApi.Object,
            NullLogger<SyncBnetCharactersCommandHandler>.Instance);

        _characters.Setup(r => r.UpsertAsync(It.IsAny<Character>(), default))
            .ReturnsAsync((Character c, CancellationToken _) => c);
        _characters.Setup(r => r.GetByUserWithDetailsAsync(It.IsAny<string>(), It.IsAny<bool>(), default))
            .ReturnsAsync([]);
        _realms.Setup(r => r.GetBySlugAndBranchAsync(It.IsAny<string>(), BranchId, default))
            .ReturnsAsync(ExistingRealm);
    }

    // ── Guard clauses ────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_NoLinkedBnetAccounts_ReturnsBnetNotLinked()
    {
        _bnetAccounts.Setup(r => r.GetAllByDiscordIdAsync(DiscordId, default))
            .ReturnsAsync((IReadOnlyList<BattleNetAccount>)[]);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.BnetNotLinked);
    }

    [Fact]
    public async Task HandleAsync_BranchNotFound_ReturnsBranchNotFound()
    {
        _bnetAccounts.Setup(r => r.GetAllByDiscordIdAsync(DiscordId, default)).ReturnsAsync([Account]);
        _branches.Setup(r => r.GetByIdAsync(BranchId, default)).ReturnsAsync((Branch?)null);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.BranchNotFound);
    }

    // ── Realm resolution ─────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_ExistingRealm_SkipsRealmCreation()
    {
        ArrangeHappyPath([BnetChar("kazzak")]);

        await _sut.HandleAsync(Command);

        _realms.Verify(r => r.AddAsync(It.IsAny<Realm>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_NewRealm_CreatesRealm()
    {
        ArrangeHappyPath([BnetChar("kazzak")]);
        _realms.Setup(r => r.GetBySlugAndBranchAsync("kazzak", BranchId, default))
            .ReturnsAsync((Realm?)null);
        _realms.Setup(r => r.AddAsync(It.IsAny<Realm>(), default))
            .ReturnsAsync((Realm realm, CancellationToken _) => realm);

        await _sut.HandleAsync(Command);

        _realms.Verify(r => r.AddAsync(
            It.Is<Realm>(realm => realm.Slug == "kazzak" && realm.Region == "eu"),
            default), Times.Once);
    }

    // ── Sync count & namespace ───────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_MultipleCharactersAcrossWowAccounts_ReturnsCorrectCount()
    {
        _bnetAccounts.Setup(r => r.GetAllByDiscordIdAsync(DiscordId, default)).ReturnsAsync([Account]);
        _branches.Setup(r => r.GetByIdAsync(BranchId, default)).ReturnsAsync(Branch);
        _bnetApi.Setup(r => r.GetWowCharactersAsync(Account.AccessToken, Account.Region, It.IsAny<string>(), default))
            .ReturnsAsync(new BnetWowAccountsResponse
            {
                WowAccounts =
                [
                    new BnetWowAccountDto { Id = 1, Characters = [BnetChar("kazzak", id: 1), BnetChar("kazzak", id: 2)] },
                    new BnetWowAccountDto { Id = 2, Characters = [BnetChar("silvermoon", id: 3)] },
                ],
            });

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Message.Should().StartWith("3");
        _characters.Verify(r => r.UpsertExpansionStateAsync(It.IsAny<CharacterExpansionState>(), default), Times.Exactly(3));
    }

    [Fact]
    public async Task HandleAsync_BuildsCorrectProfileNamespace()
    {
        ArrangeHappyPath([]);

        await _sut.HandleAsync(Command);

        // "dynamic" → "profile-eu"
        _bnetApi.Verify(r => r.GetWowCharactersAsync(Account.AccessToken, Account.Region, "profile-eu", default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ClassicBranch_BuildsCorrectProfileNamespace()
    {
        var classicBranch = new Branch { Id = BranchId, Name = "Classic Era", BnetNamespacePrefix = "dynamic-classic1x", CurrentExpansionId = 2 };
        _bnetAccounts.Setup(r => r.GetAllByDiscordIdAsync(DiscordId, default)).ReturnsAsync([Account]);
        _branches.Setup(r => r.GetByIdAsync(BranchId, default)).ReturnsAsync(classicBranch);
        _bnetApi.Setup(r => r.GetWowCharactersAsync(Account.AccessToken, Account.Region, It.IsAny<string>(), default))
            .ReturnsAsync(new BnetWowAccountsResponse { WowAccounts = [] });

        await _sut.HandleAsync(Command);

        // "dynamic-classic1x" → "profile-classic1x-eu"
        _bnetApi.Verify(r => r.GetWowCharactersAsync(Account.AccessToken, Account.Region, "profile-classic1x-eu", default), Times.Once);
    }

    // ── Multi-account looping ────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_MultipleLinkedAccounts_QueriesBnetApiOncePerAccountWithItsOwnToken()
    {
        _bnetAccounts.Setup(r => r.GetAllByDiscordIdAsync(DiscordId, default)).ReturnsAsync([Account, SecondAccount]);
        _branches.Setup(r => r.GetByIdAsync(BranchId, default)).ReturnsAsync(Branch);
        _bnetApi.Setup(r => r.GetWowCharactersAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(new BnetWowAccountsResponse { WowAccounts = [] });

        await _sut.HandleAsync(Command);

        _bnetApi.Verify(r => r.GetWowCharactersAsync(Account.AccessToken, Account.Region, "profile-eu", default), Times.Once);
        _bnetApi.Verify(r => r.GetWowCharactersAsync(SecondAccount.AccessToken, SecondAccount.Region, "profile-us", default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_MultipleLinkedAccounts_AggregatesSyncedCountAcrossAccounts()
    {
        _bnetAccounts.Setup(r => r.GetAllByDiscordIdAsync(DiscordId, default)).ReturnsAsync([Account, SecondAccount]);
        _branches.Setup(r => r.GetByIdAsync(BranchId, default)).ReturnsAsync(Branch);
        _bnetApi.Setup(r => r.GetWowCharactersAsync(Account.AccessToken, Account.Region, It.IsAny<string>(), default))
            .ReturnsAsync(new BnetWowAccountsResponse
            {
                WowAccounts = [new BnetWowAccountDto { Id = 1, Characters = [BnetChar("kazzak", id: 1)] }],
            });
        _bnetApi.Setup(r => r.GetWowCharactersAsync(SecondAccount.AccessToken, SecondAccount.Region, It.IsAny<string>(), default))
            .ReturnsAsync(new BnetWowAccountsResponse
            {
                WowAccounts = [new BnetWowAccountDto { Id = 2, Characters = [BnetChar("silvermoon", id: 2), BnetChar("silvermoon", id: 3)] }],
            });

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Message.Should().StartWith("3");
    }

    [Fact]
    public async Task HandleAsync_MultipleLinkedAccounts_TagsEachCharacterWithItsSourceAccount()
    {
        _bnetAccounts.Setup(r => r.GetAllByDiscordIdAsync(DiscordId, default)).ReturnsAsync([Account, SecondAccount]);
        _branches.Setup(r => r.GetByIdAsync(BranchId, default)).ReturnsAsync(Branch);
        _bnetApi.Setup(r => r.GetWowCharactersAsync(Account.AccessToken, Account.Region, It.IsAny<string>(), default))
            .ReturnsAsync(new BnetWowAccountsResponse
            {
                WowAccounts = [new BnetWowAccountDto { Id = 1, Characters = [BnetChar("kazzak", id: 1)] }],
            });
        _bnetApi.Setup(r => r.GetWowCharactersAsync(SecondAccount.AccessToken, SecondAccount.Region, It.IsAny<string>(), default))
            .ReturnsAsync(new BnetWowAccountsResponse
            {
                WowAccounts = [new BnetWowAccountDto { Id = 2, Characters = [BnetChar("silvermoon", id: 2)] }],
            });

        await _sut.HandleAsync(Command);

        _characters.Verify(r => r.UpsertAsync(
            It.Is<Character>(c => c.BnetCharacterId == 1 && c.SourceBnetId == Account.BnetId), default), Times.Once);
        _characters.Verify(r => r.UpsertAsync(
            It.Is<Character>(c => c.BnetCharacterId == 2 && c.SourceBnetId == SecondAccount.BnetId), default), Times.Once);
    }

    // ── Preserving enrichment data not returned by the character-list endpoint ──

    [Fact]
    public async Task HandleAsync_ExistingCharacterWithAvatarUrl_PreservesItOnUpsert()
    {
        const long bnetCharacterId = 1001;
        ArrangeHappyPath([BnetChar("kazzak", id: bnetCharacterId)]);
        _characters.Setup(r => r.GetByUserWithDetailsAsync(DiscordId, false, default)).ReturnsAsync(
        [
            new Character
            {
                Id = 42, BnetCharacterId = bnetCharacterId, BranchId = BranchId,
                AvatarUrl = "https://cdn/avatar.jpg", ExpansionStates = [],
            },
        ]);

        await _sut.HandleAsync(Command);

        _characters.Verify(r => r.UpsertAsync(
            It.Is<Character>(c => c.BnetCharacterId == bnetCharacterId && c.AvatarUrl == "https://cdn/avatar.jpg"),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ExistingExpansionState_PreservesGuildNameAndItemLevel()
    {
        const long bnetCharacterId = 1001;
        ArrangeHappyPath([BnetChar("kazzak", id: bnetCharacterId)]);
        _characters.Setup(r => r.GetByUserWithDetailsAsync(DiscordId, false, default)).ReturnsAsync(
        [
            new Character
            {
                Id = 42, BnetCharacterId = bnetCharacterId, BranchId = BranchId,
                ExpansionStates =
                [
                    new CharacterExpansionState
                    {
                        CharacterId = 42, ExpansionId = Branch.CurrentExpansionId,
                        GuildName = "Existing Guild", ItemLevel = 615,
                    },
                ],
            },
        ]);

        CharacterExpansionState? upserted = null;
        _characters.Setup(r => r.UpsertExpansionStateAsync(It.IsAny<CharacterExpansionState>(), default))
            .Callback<CharacterExpansionState, CancellationToken>((s, _) => upserted = s)
            .Returns(Task.CompletedTask);

        await _sut.HandleAsync(Command);

        upserted.Should().NotBeNull();
        upserted!.GuildName.Should().Be("Existing Guild");
        upserted.ItemLevel.Should().Be(615);
    }

    [Fact]
    public async Task HandleAsync_NoExistingCharacter_LeavesAvatarGuildNameAndItemLevelNull()
    {
        ArrangeHappyPath([BnetChar("kazzak")]);

        CharacterExpansionState? upserted = null;
        _characters.Setup(r => r.UpsertExpansionStateAsync(It.IsAny<CharacterExpansionState>(), default))
            .Callback<CharacterExpansionState, CancellationToken>((s, _) => upserted = s)
            .Returns(Task.CompletedTask);

        await _sut.HandleAsync(Command);

        _characters.Verify(r => r.UpsertAsync(It.Is<Character>(c => c.AvatarUrl == null), default), Times.Once);
        upserted!.GuildName.Should().BeNull();
        upserted.ItemLevel.Should().BeNull();
    }

    // ── Faction & gender mapping ─────────────────────────────────────────────

    [Theory]
    [InlineData("ALLIANCE", Faction.Alliance)]
    [InlineData("HORDE",    Faction.Horde)]
    [InlineData("PANDAREN", Faction.Neutral)]
    public async Task HandleAsync_FactionString_MapsCorrectly(string factionString, Faction expected)
    {
        ArrangeHappyPath([BnetChar("kazzak", faction: factionString)]);

        await _sut.HandleAsync(Command);

        _characters.Verify(r => r.UpsertAsync(
            It.Is<Character>(c => c.Faction == expected), default), Times.Once);
    }

    [Theory]
    [InlineData("FEMALE", Gender.Female)]
    [InlineData("MALE",   Gender.Male)]
    [InlineData("UNKNOWN", Gender.Male)]
    public async Task HandleAsync_GenderString_MapsCorrectly(string genderString, Gender expected)
    {
        ArrangeHappyPath([BnetChar("kazzak", gender: genderString)]);

        await _sut.HandleAsync(Command);

        _characters.Verify(r => r.UpsertAsync(
            It.Is<Character>(c => c.Gender == expected), default), Times.Once);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void ArrangeHappyPath(IEnumerable<BnetWowCharacterDto> characters)
    {
        _bnetAccounts.Setup(r => r.GetAllByDiscordIdAsync(DiscordId, default)).ReturnsAsync([Account]);
        _branches.Setup(r => r.GetByIdAsync(BranchId, default)).ReturnsAsync(Branch);
        _bnetApi.Setup(r => r.GetWowCharactersAsync(Account.AccessToken, Account.Region, It.IsAny<string>(), default))
            .ReturnsAsync(new BnetWowAccountsResponse
            {
                WowAccounts = [new BnetWowAccountDto { Id = 1, Characters = [.. characters] }],
            });
    }

    private static BnetWowCharacterDto BnetChar(
        string realmSlug,
        long   id      = 1001,
        string faction = "ALLIANCE",
        string gender  = "MALE") => new()
    {
        Id    = id,
        Name  = "Arthas",
        Level = 80,
        Realm         = new BnetRealmRefDto { Slug = realmSlug, Name = realmSlug },
        PlayableClass = new BnetIdRefDto    { Id = 1, Name = "Warrior" },
        PlayableRace  = new BnetIdRefDto    { Id = 1, Name = "Human" },
        Gender        = new BnetTypeRefDto  { Type = gender },
        Faction       = new BnetTypeRefDto  { Type = faction },
    };
}
