using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RaidOps.Application.Contracts.Characters.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Characters.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Reference;
using RaidOps.ExternalApplication.Contracts.Services.BNet;
using RaidOps.ExternalApplication.Contracts.Services.BNet.Responses;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Characters.CommandHandlers;

public class ActivateCharactersCommandHandlerTests
{
    private readonly Mock<ICharacterRepository>  _characters   = new();
    private readonly Mock<IBnetAccountRepository> _bnetAccounts = new();
    private readonly Mock<IBnetApiService>        _bnetApi      = new();
    private readonly Mock<ISpecResolverService>   _specResolver = new();
    private readonly ActivateCharactersCommandHandler _sut;

    private const string DiscordId   = "user-1";
    private const int    CharacterId = 10;

    private static readonly ActivateCharactersCommand Command = new()
    {
        UserDiscordId = DiscordId,
        CharacterIds  = [CharacterId],
    };

    private static readonly BattleNetAccount Account = new()
    {
        UserDiscordId = DiscordId,
        BnetId        = "bnet-1",
        AccessToken   = "tok",
        Region        = "eu",
        BattleTag     = "Player#1234",
    };

    public ActivateCharactersCommandHandlerTests()
    {
        _sut = new ActivateCharactersCommandHandler(
            _characters.Object,
            _bnetAccounts.Object,
            _bnetApi.Object,
            _specResolver.Object,
            NullLogger<ActivateCharactersCommandHandler>.Instance);

        _specResolver.Setup(s => s.ResolveAsync(
                It.IsAny<BnetCharacterSpecializationsResponse>(),
                It.IsAny<int>(),
                It.IsAny<CharacterExpansionState>(),
                default))
            .ReturnsAsync([]);

        _characters.Setup(r => r.UpsertAsync(It.IsAny<Character>(), default))
            .ReturnsAsync((Character c, CancellationToken _) => c);

        _bnetApi.Setup(b => b.GetAppTokenAsync(It.IsAny<string>(), default))
            .ReturnsAsync("app-token");
    }

    // ── No BNet account ───────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_AppTokenFetchFails_ActivatesWithoutEnrichment()
    {
        _bnetAccounts.Setup(r => r.GetByDiscordIdAsync(DiscordId, default)).ReturnsAsync(Account);
        _characters.Setup(r => r.GetByIdsWithDetailsAsync(Command.CharacterIds, DiscordId, default))
            .ReturnsAsync([MakeCharacter()]);
        _bnetApi.Setup(b => b.GetAppTokenAsync(It.IsAny<string>(), default))
            .ThrowsAsync(new HttpRequestException("token endpoint unreachable"));

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _bnetApi.Verify(b => b.GetCharacterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
        _characters.Verify(r => r.UpsertAsync(It.IsAny<Character>(), default), Times.Never);
        _characters.Verify(r => r.ActivateAsync(Command.CharacterIds, DiscordId, default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_NoBnetAccount_ActivatesWithoutEnrichment()
    {
        _bnetAccounts.Setup(r => r.GetByDiscordIdAsync(DiscordId, default))
            .ReturnsAsync((BattleNetAccount?)null);
        _characters.Setup(r => r.GetByIdsWithDetailsAsync(Command.CharacterIds, DiscordId, default))
            .ReturnsAsync([MakeCharacter()]);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _bnetApi.Verify(b => b.GetCharacterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
        _characters.Verify(r => r.ActivateAsync(Command.CharacterIds, DiscordId, default), Times.Once);
    }

    // ── BNet API succeeds ────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_BnetApiSucceeds_EnrichesCharacterThenActivates()
    {
        _bnetAccounts.Setup(r => r.GetByDiscordIdAsync(DiscordId, default)).ReturnsAsync(Account);
        _characters.Setup(r => r.GetByIdsWithDetailsAsync(Command.CharacterIds, DiscordId, default))
            .ReturnsAsync([MakeCharacter()]);

        _bnetApi.Setup(b => b.GetCharacterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(new BnetCharacterDetailResponse { Level = 80, EquippedItemLevel = 600, Guild = new BnetGuildRefDto { Name = "RaidOps" } });
        _bnetApi.Setup(b => b.GetCharacterMediaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(new BnetCharacterMediaResponse { Assets = [new BnetMediaAssetDto { Key = "avatar", Value = "https://cdn/avatar.jpg" }] });
        _bnetApi.Setup(b => b.GetCharacterSpecializationsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(new BnetCharacterSpecializationsResponse());

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _characters.Verify(r => r.UpsertAsync(It.Is<Character>(c => c.AvatarUrl == "https://cdn/avatar.jpg"), default), Times.Once);
        _characters.Verify(r => r.UpsertExpansionStateAsync(It.Is<CharacterExpansionState>(s => s.Level == 80 && s.GuildName == "RaidOps"), default), Times.Once);
        _characters.Verify(r => r.ActivateAsync(Command.CharacterIds, DiscordId, default), Times.Once);
    }

    // ── BNet API throws ───────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_BnetApiThrows_ActivatesWithoutEnrichment()
    {
        _bnetAccounts.Setup(r => r.GetByDiscordIdAsync(DiscordId, default)).ReturnsAsync(Account);
        _characters.Setup(r => r.GetByIdsWithDetailsAsync(Command.CharacterIds, DiscordId, default))
            .ReturnsAsync([MakeCharacter()]);

        _bnetApi.Setup(b => b.GetCharacterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ThrowsAsync(new HttpRequestException("BNet unreachable"));

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _characters.Verify(r => r.UpsertAsync(It.IsAny<Character>(), default), Times.Never);
        _characters.Verify(r => r.ActivateAsync(Command.CharacterIds, DiscordId, default), Times.Once);
    }

    // ── Enrichment edge cases ────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_ZeroEquippedItemLevel_SetsItemLevelToNull()
    {
        _bnetAccounts.Setup(r => r.GetByDiscordIdAsync(DiscordId, default)).ReturnsAsync(Account);
        _characters.Setup(r => r.GetByIdsWithDetailsAsync(Command.CharacterIds, DiscordId, default))
            .ReturnsAsync([MakeCharacter()]);

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
    public async Task HandleAsync_ExistingExpansionState_UpdatesItInPlace()
    {
        var existingState = new CharacterExpansionState { Id = 99, CharacterId = CharacterId, ExpansionId = 10, Level = 60 };
        var character = MakeCharacter();
        character.ExpansionStates = [existingState];

        _bnetAccounts.Setup(r => r.GetByDiscordIdAsync(DiscordId, default)).ReturnsAsync(Account);
        _characters.Setup(r => r.GetByIdsWithDetailsAsync(Command.CharacterIds, DiscordId, default))
            .ReturnsAsync([character]);

        _bnetApi.Setup(b => b.GetCharacterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(new BnetCharacterDetailResponse { Level = 80, EquippedItemLevel = 600 });
        _bnetApi.Setup(b => b.GetCharacterMediaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(new BnetCharacterMediaResponse());
        _bnetApi.Setup(b => b.GetCharacterSpecializationsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(new BnetCharacterSpecializationsResponse());

        await _sut.HandleAsync(Command);

        // The existing state (Id 99) is reused and updated — not a new one
        _characters.Verify(r => r.UpsertExpansionStateAsync(
            It.Is<CharacterExpansionState>(s => s.Id == 99 && s.Level == 80), default), Times.Once);
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
        ExpansionStates = [],
    };
}
