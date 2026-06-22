using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Characters.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Characters.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Reference;
using RaidOps.ExternalApplication.Contracts.Services.BNet;
using RaidOps.ExternalApplication.Contracts.Services.BNet.Responses;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Characters.CommandHandlers;

public class ResyncCharacterCommandHandlerTests
{
    private readonly Mock<ICharacterRepository>   _characters   = new();
    private readonly Mock<IBnetAccountRepository>  _bnetAccounts = new();
    private readonly Mock<IBnetApiService>         _bnetApi      = new();
    private readonly Mock<ISpecResolverService>    _specResolver = new();
    private readonly ResyncCharacterCommandHandler _sut;

    private const string DiscordId   = "user-1";
    private const int    CharacterId = 10;

    private static readonly ResyncCharacterCommand Command = new()
    {
        UserDiscordId = DiscordId,
        CharacterId   = CharacterId,
    };

    private static readonly BattleNetAccount Account = new()
    {
        UserDiscordId = DiscordId,
        BnetId        = "bnet-1",
        AccessToken   = "tok",
        Region        = "eu",
        BattleTag     = "Player#1234",
    };

    public ResyncCharacterCommandHandlerTests()
    {
        _sut = new ResyncCharacterCommandHandler(
            _characters.Object,
            _bnetAccounts.Object,
            _bnetApi.Object,
            _specResolver.Object);

        _specResolver.Setup(s => s.ResolveAsync(
                It.IsAny<BnetCharacterSpecializationsResponse>(),
                It.IsAny<int>(),
                It.IsAny<CharacterExpansionState>(),
                default))
            .ReturnsAsync([]);

        _characters.Setup(r => r.UpsertAsync(It.IsAny<Character>(), default))
            .ReturnsAsync((Character c, CancellationToken _) => c);
    }

    // ── Guard clause ─────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_CharacterNotFound_ReturnsNotFound()
    {
        _characters.Setup(r => r.GetByUserWithDetailsAsync(DiscordId, true, default))
            .ReturnsAsync([]);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.NotFound);
    }

    // ── No BNet account ───────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_NoBnetAccount_ReturnsOkWithoutEnrichment()
    {
        var character = MakeCharacter();
        _characters.Setup(r => r.GetByUserWithDetailsAsync(DiscordId, true, default))
            .ReturnsAsync([character]);
        _bnetAccounts.Setup(r => r.GetByDiscordIdAsync(DiscordId, default))
            .ReturnsAsync((BattleNetAccount?)null);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _bnetApi.Verify(b => b.GetCharacterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
        _characters.Verify(r => r.UpsertAsync(It.IsAny<Character>(), default), Times.Never);
    }

    // ── BNet API throws ───────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_GetAppTokenThrows_ReturnsBnetApiError()
    {
        _characters.Setup(r => r.GetByUserWithDetailsAsync(DiscordId, true, default))
            .ReturnsAsync([MakeCharacter()]);
        _bnetAccounts.Setup(r => r.GetByDiscordIdAsync(DiscordId, default)).ReturnsAsync(Account);

        _bnetApi.Setup(b => b.GetAppTokenAsync(It.IsAny<string>(), default))
            .ThrowsAsync(new HttpRequestException("BNet unreachable"));

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.BnetApiError);
        _bnetApi.Verify(b => b.GetCharacterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_BnetApiThrows_ReturnsBnetApiError()
    {
        _characters.Setup(r => r.GetByUserWithDetailsAsync(DiscordId, true, default))
            .ReturnsAsync([MakeCharacter()]);
        _bnetAccounts.Setup(r => r.GetByDiscordIdAsync(DiscordId, default)).ReturnsAsync(Account);

        _bnetApi.Setup(b => b.GetCharacterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ThrowsAsync(new HttpRequestException("BNet unreachable"));

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.BnetApiError);
    }

    // ── Happy path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_Success_EnrichesAndReturnsDtoWithPayload()
    {
        var character = MakeCharacter();
        _characters.Setup(r => r.GetByUserWithDetailsAsync(DiscordId, true, default))
            .ReturnsAsync([character]);
        _bnetAccounts.Setup(r => r.GetByDiscordIdAsync(DiscordId, default)).ReturnsAsync(Account);

        _bnetApi.Setup(b => b.GetCharacterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(new BnetCharacterDetailResponse { Level = 80, EquippedItemLevel = 600 });
        _bnetApi.Setup(b => b.GetCharacterMediaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(new BnetCharacterMediaResponse { Assets = [new BnetMediaAssetDto { Key = "avatar", Value = "https://cdn/avatar.jpg" }] });
        _bnetApi.Setup(b => b.GetCharacterSpecializationsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(new BnetCharacterSpecializationsResponse());

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Body.Should().NotBeNull();
        _characters.Verify(r => r.UpsertAsync(It.Is<Character>(c => c.AvatarUrl == "https://cdn/avatar.jpg"), default), Times.Once);
        _characters.Verify(r => r.UpsertExpansionStateAsync(It.Is<CharacterExpansionState>(s => s.Level == 80), default), Times.Once);
    }

    // ── Enrichment edge cases ────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_ZeroEquippedItemLevel_SetsItemLevelToNull()
    {
        _characters.Setup(r => r.GetByUserWithDetailsAsync(DiscordId, true, default))
            .ReturnsAsync([MakeCharacter()]);
        _bnetAccounts.Setup(r => r.GetByDiscordIdAsync(DiscordId, default)).ReturnsAsync(Account);

        _bnetApi.Setup(b => b.GetCharacterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(new BnetCharacterDetailResponse { Level = 80, EquippedItemLevel = 0 });
        _bnetApi.Setup(b => b.GetCharacterMediaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(new BnetCharacterMediaResponse());
        _bnetApi.Setup(b => b.GetCharacterSpecializationsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(new BnetCharacterSpecializationsResponse());

        await _sut.HandleAsync(Command);

        _characters.Verify(r => r.UpsertExpansionStateAsync(
            It.Is<CharacterExpansionState>(s => s.ItemLevel == null), default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_NoMatchingExpansionState_CreatesNewStateWithGuildName()
    {
        var character = MakeCharacter();
        character.ExpansionStates = []; // no state for expansionId 10

        _characters.Setup(r => r.GetByUserWithDetailsAsync(DiscordId, true, default))
            .ReturnsAsync([character]);
        _bnetAccounts.Setup(r => r.GetByDiscordIdAsync(DiscordId, default)).ReturnsAsync(Account);

        _bnetApi.Setup(b => b.GetCharacterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(new BnetCharacterDetailResponse { Level = 80, EquippedItemLevel = 600, Guild = new BnetGuildRefDto { Name = "RaidOps" } });
        _bnetApi.Setup(b => b.GetCharacterMediaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(new BnetCharacterMediaResponse());
        _bnetApi.Setup(b => b.GetCharacterSpecializationsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(new BnetCharacterSpecializationsResponse());

        await _sut.HandleAsync(Command);

        // New state created (Id == 0) with CharacterId, ExpansionId, Level and GuildName populated
        _characters.Verify(r => r.UpsertExpansionStateAsync(
            It.Is<CharacterExpansionState>(s =>
                s.Id          == 0          &&
                s.CharacterId == CharacterId &&
                s.ExpansionId == 10          &&
                s.Level       == 80          &&
                s.GuildName   == "RaidOps"),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ExistingExpansionState_UpdatesItInPlace()
    {
        var existingState = new CharacterExpansionState { Id = 55, CharacterId = CharacterId, ExpansionId = 10, Level = 60 };
        var character = MakeCharacter();
        character.ExpansionStates = [existingState];

        _characters.Setup(r => r.GetByUserWithDetailsAsync(DiscordId, true, default))
            .ReturnsAsync([character]);
        _bnetAccounts.Setup(r => r.GetByDiscordIdAsync(DiscordId, default)).ReturnsAsync(Account);

        _bnetApi.Setup(b => b.GetCharacterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(new BnetCharacterDetailResponse { Level = 80, EquippedItemLevel = 600 });
        _bnetApi.Setup(b => b.GetCharacterMediaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(new BnetCharacterMediaResponse());
        _bnetApi.Setup(b => b.GetCharacterSpecializationsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(new BnetCharacterSpecializationsResponse());

        await _sut.HandleAsync(Command);

        // Existing state (Id 55) is reused, not replaced by a new one
        _characters.Verify(r => r.UpsertExpansionStateAsync(
            It.Is<CharacterExpansionState>(s => s.Id == 55 && s.Level == 80), default), Times.Once);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Character MakeCharacter() => new()
    {
        Id            = CharacterId,
        Name          = "Arthas",
        Faction       = Faction.Alliance,
        UserDiscordId = DiscordId,
        ClassId       = 6,
        Branch  = new Branch { Id = 1, Name = "Retail", BnetNamespacePrefix = "dynamic", CurrentExpansionId = 10 },
        Realm   = new Realm  { Id = 1, Name = "Kazzak", Slug = "kazzak", Region = "eu", BranchId = 1 },
        Class   = new WowClass { Id = 6, Name = "Death Knight", Color = "C41F3B" },
        Race    = new Race { Id = 1, Name = "Human" },
        ExpansionStates = [new CharacterExpansionState { ExpansionId = 10, Level = 70, IsActive = true }],
    };
}
