using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RaidOps.Application.Contracts.Characters.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Characters.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Characters.CommandHandlers;

/// <summary>
/// Unit tests for <see cref="SetCharacterRaidSpecsCommandHandler"/>.
/// </summary>
public class SetCharacterRaidSpecsCommandHandlerTests
{
    private readonly Mock<ICharacterRepository>       _characters  = new();
    private readonly Mock<ISpecRepository>            _specs       = new();
    private readonly Mock<IGuildMembershipRepository> _memberships = new();
    private readonly Mock<IGuildAccessService>        _guildAccess = new();
    private readonly SetCharacterRaidSpecsCommandHandler _sut;

    private const int    CharacterId = 1;
    private const int    ClassId     = 1; // Warrior
    private const string DiscordId   = "user-1";
    private const string OwnerId     = "owner-1";
    private const string GuildId     = "guild-1";

    public SetCharacterRaidSpecsCommandHandlerTests()
    {
        _sut = new SetCharacterRaidSpecsCommandHandler(_characters.Object, _specs.Object, _memberships.Object, _guildAccess.Object, NullLogger<SetCharacterRaidSpecsCommandHandler>.Instance);
    }

    private static SetCharacterRaidSpecsCommand MakeCommand(int mainSpecId, IEnumerable<int> viableSpecIds) => new()
    {
        UserDiscordId = DiscordId,
        CharacterId   = CharacterId,
        MainSpecId    = mainSpecId,
        ViableSpecIds = viableSpecIds,
    };

    // ── Validation ────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_EmptyViableSpecIds_ReturnsInvalidRequest()
    {
        var result = await _sut.HandleAsync(MakeCommand(71, []));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidRequest);
        _characters.Verify(r => r.GetByIdAsync(It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_MainSpecNotInViableSpecIds_ReturnsInvalidRequest()
    {
        var result = await _sut.HandleAsync(MakeCommand(73, [71, 72]));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidRequest);
        _characters.Verify(r => r.GetByIdAsync(It.IsAny<int>(), default), Times.Never);
    }

    // ── Ownership / officer access ────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_CharacterNotFound_ReturnsCharacterNotFound()
    {
        _characters.Setup(r => r.GetByIdAsync(CharacterId, default)).ReturnsAsync((Character?)null);

        var result = await _sut.HandleAsync(MakeCommand(71, [71]));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.CharacterNotFound);
    }

    [Fact]
    public async Task HandleAsync_NotOwnerAndNotOfficer_ReturnsForbidden()
    {
        _characters.Setup(r => r.GetByIdAsync(CharacterId, default))
            .ReturnsAsync(new Character { Id = CharacterId, ClassId = ClassId, UserDiscordId = OwnerId });
        _memberships.Setup(r => r.GetByCharacterIdAsync(CharacterId, default))
            .ReturnsAsync([new GuildMembership { CharacterId = CharacterId, GuildId = GuildId }]);
        _guildAccess.Setup(a => a.GetAccessLevelAsync(DiscordId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(MakeCommand(71, [71]));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
        _characters.Verify(r => r.UpsertRaidSpecsAsync(It.IsAny<int>(), It.IsAny<IEnumerable<CharacterRaidSpec>>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_NotOwnerAndNoSharedGuild_ReturnsForbidden()
    {
        _characters.Setup(r => r.GetByIdAsync(CharacterId, default))
            .ReturnsAsync(new Character { Id = CharacterId, ClassId = ClassId, UserDiscordId = OwnerId });
        _memberships.Setup(r => r.GetByCharacterIdAsync(CharacterId, default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(MakeCommand(71, [71]));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    // ── Spec validation ───────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_SpecDoesNotExist_ReturnsInvalidRequest()
    {
        SetupCharacter();
        _specs.Setup(r => r.GetAllAsync(default)).ReturnsAsync([MakeSpec(71, ClassId)]);

        var result = await _sut.HandleAsync(MakeCommand(71, [71, 999]));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidRequest);
        _characters.Verify(r => r.UpsertRaidSpecsAsync(It.IsAny<int>(), It.IsAny<IEnumerable<CharacterRaidSpec>>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SpecBelongsToDifferentClass_ReturnsInvalidRequest()
    {
        SetupCharacter();
        _specs.Setup(r => r.GetAllAsync(default)).ReturnsAsync([MakeSpec(71, ClassId), MakeSpec(62, classId: 8)]);

        var result = await _sut.HandleAsync(MakeCommand(71, [71, 62]));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidRequest);
        _characters.Verify(r => r.UpsertRaidSpecsAsync(It.IsAny<int>(), It.IsAny<IEnumerable<CharacterRaidSpec>>(), default), Times.Never);
    }

    // ── Success — owner ───────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_Success_PersistsExactlyOneMainSpec()
    {
        SetupCharacter();
        _specs.Setup(r => r.GetAllAsync(default)).ReturnsAsync([MakeSpec(71, ClassId), MakeSpec(72, ClassId), MakeSpec(73, ClassId)]);

        List<CharacterRaidSpec>? persisted = null;
        _characters.Setup(r => r.UpsertRaidSpecsAsync(CharacterId, It.IsAny<IEnumerable<CharacterRaidSpec>>(), default))
            .Callback<int, IEnumerable<CharacterRaidSpec>, CancellationToken>((_, specs, _) => persisted = specs.ToList())
            .Returns(Task.CompletedTask);

        var result = await _sut.HandleAsync(MakeCommand(72, [71, 72, 73]));

        result.IsSuccess.Should().BeTrue();
        persisted.Should().HaveCount(3);
        persisted.Should().ContainSingle(s => s.IsMain).Which.SpecId.Should().Be(72);
        persisted!.Where(s => !s.IsMain).Select(s => s.SpecId).Should().BeEquivalentTo([71, 73]);
        persisted.Should().OnlyContain(s => s.CharacterId == CharacterId);
        _memberships.Verify(r => r.GetByCharacterIdAsync(It.IsAny<int>(), default), Times.Never);
    }

    // ── Success — officer editing someone else's raid specs ──────────────

    [Fact]
    public async Task HandleAsync_OfficerNotOwner_PersistsSpecs()
    {
        _characters.Setup(r => r.GetByIdAsync(CharacterId, default))
            .ReturnsAsync(new Character { Id = CharacterId, ClassId = ClassId, UserDiscordId = OwnerId });
        _memberships.Setup(r => r.GetByCharacterIdAsync(CharacterId, default))
            .ReturnsAsync([new GuildMembership { CharacterId = CharacterId, GuildId = GuildId }]);
        _guildAccess.Setup(a => a.GetAccessLevelAsync(DiscordId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _specs.Setup(r => r.GetAllAsync(default)).ReturnsAsync([MakeSpec(71, ClassId)]);

        var result = await _sut.HandleAsync(MakeCommand(71, [71]));

        result.IsSuccess.Should().BeTrue();
        _characters.Verify(r => r.UpsertRaidSpecsAsync(CharacterId, It.IsAny<IEnumerable<CharacterRaidSpec>>(), default), Times.Once);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void SetupCharacter() =>
        _characters.Setup(r => r.GetByIdAsync(CharacterId, default))
            .ReturnsAsync(new Character { Id = CharacterId, ClassId = ClassId, UserDiscordId = DiscordId });

    private static Spec MakeSpec(int id, int classId) => new() { Id = id, Name = $"Spec{id}", ClassId = classId };
}
