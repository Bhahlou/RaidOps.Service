using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Guilds.Memberships.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Guilds.Memberships.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Discord;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Guilds.Memberships.CommandHandlers;

/// <summary>
/// Unit tests for <see cref="UpdateCharacterRankCommandHandler"/>.
/// </summary>
public class UpdateCharacterRankCommandHandlerTests
{
    private readonly Mock<ICharacterRepository>       _characters  = new();
    private readonly Mock<IGuildMembershipRepository> _memberships = new();
    private readonly Mock<IAuditLogService>           _auditLog    = new();
    private readonly UpdateCharacterRankCommandHandler _sut;

    private const int    CharacterId = 1;
    private const string GuildId     = "guild-1";
    private const string DiscordId   = "user-1";

    private static readonly UpdateCharacterRankCommand Command = new()
    {
        CharacterId        = CharacterId,
        GuildId            = GuildId,
        RequesterDiscordId = DiscordId,
        CharacterRank      = CharacterRank.Alt,
    };

    public UpdateCharacterRankCommandHandlerTests()
    {
        _sut = new UpdateCharacterRankCommandHandler(_characters.Object, _memberships.Object, _auditLog.Object);
    }

    // ── CharacterNotFound ─────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_CharacterNotFound_ReturnsCharacterNotFound()
    {
        _characters.Setup(r => r.GetByIdAsync(CharacterId, default)).ReturnsAsync((Character?)null);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.CharacterNotFound);
    }

    // ── CharacterNotOwned ─────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_CharacterNotOwned_ReturnsCharacterNotOwned()
    {
        _characters.Setup(r => r.GetByIdAsync(CharacterId, default))
            .ReturnsAsync(new Character { Id = CharacterId, Name = "Arthas", UserDiscordId = "other-user" });

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.CharacterNotOwned);
    }

    // ── NotAMember ────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_NotAMember_ReturnsNotAMember()
    {
        SetupCharacter();
        _memberships.Setup(r => r.GetAsync(CharacterId, GuildId, default)).ReturnsAsync((GuildMembership?)null);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.NotAMember);
    }

    // ── RankUnchanged ─────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_RankUnchanged_ReturnsOkWithoutUpdating()
    {
        SetupCharacter();
        _memberships.Setup(r => r.GetAsync(CharacterId, GuildId, default))
            .ReturnsAsync(new GuildMembership
            {
                CharacterId   = CharacterId,
                GuildId       = GuildId,
                CharacterRank = CharacterRank.Alt, // same as command
            });

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _memberships.Verify(r => r.UpdateRankAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CharacterRank>(), default), Times.Never);
        _auditLog.Verify(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GuildAuditAction>(), It.IsAny<Dictionary<string, string>?>(), default), Times.Never);
    }

    // ── Success ───────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_Success_UpdatesAndLogs()
    {
        SetupCharacter();
        _memberships.Setup(r => r.GetAsync(CharacterId, GuildId, default))
            .ReturnsAsync(new GuildMembership
            {
                CharacterId   = CharacterId,
                GuildId       = GuildId,
                CharacterRank = CharacterRank.Main, // different from command's Alt
            });

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _memberships.Verify(r => r.UpdateRankAsync(CharacterId, GuildId, CharacterRank.Alt, default), Times.Once);
        _auditLog.Verify(a => a.LogAsync(
            GuildId, DiscordId, GuildAuditAction.MemberRankUpdated,
            It.Is<Dictionary<string, string>?>(d =>
                d != null && d["oldRank"] == "Main" && d["newRank"] == "Alt"),
            default), Times.Once);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void SetupCharacter() =>
        _characters.Setup(r => r.GetByIdAsync(CharacterId, default))
            .ReturnsAsync(new Character { Id = CharacterId, Name = "Arthas", UserDiscordId = DiscordId });
}
