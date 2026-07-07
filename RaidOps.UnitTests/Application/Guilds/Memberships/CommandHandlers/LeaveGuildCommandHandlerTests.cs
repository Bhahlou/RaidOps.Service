using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Guilds.Memberships.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Guilds.Memberships.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Guilds.Memberships.CommandHandlers;

/// <summary>
/// Unit tests for <see cref="LeaveGuildCommandHandler"/>.
/// </summary>
public class LeaveGuildCommandHandlerTests
{
    private readonly Mock<ICharacterRepository>       _characters  = new();
    private readonly Mock<IGuildMembershipRepository> _memberships = new();
    private readonly Mock<IGuildAccessService>        _guildAccess = new();
    private readonly Mock<IAuditLogService>           _auditLog    = new();
    private readonly LeaveGuildCommandHandler         _sut;

    private const int    CharacterId = 1;
    private const string GuildId     = "guild-1";
    private const string DiscordId   = "user-1";
    private const string OwnerId     = "owner-1";

    private static readonly LeaveGuildCommand Command = new()
    {
        CharacterId        = CharacterId,
        GuildId            = GuildId,
        RequesterDiscordId = DiscordId,
    };

    public LeaveGuildCommandHandlerTests()
    {
        _sut = new LeaveGuildCommandHandler(_characters.Object, _memberships.Object, _guildAccess.Object, _auditLog.Object, NullLogger<LeaveGuildCommandHandler>.Instance);
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

    // ── Not owner, not officer ───────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_NotOwnerAndNotOfficer_ReturnsForbidden()
    {
        _characters.Setup(r => r.GetByIdAsync(CharacterId, default))
            .ReturnsAsync(new Character { Id = CharacterId, Name = "Arthas", UserDiscordId = OwnerId });
        _guildAccess.Setup(a => a.GetAccessLevelAsync(DiscordId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
        _memberships.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<string>(), default), Times.Never);
    }

    // ── NotAMember ────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_NotAMember_ReturnsNotAMember()
    {
        SetupCharacter();
        _memberships.Setup(r => r.DeleteAsync(CharacterId, GuildId, default)).ReturnsAsync(false);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.NotAMember);
    }

    // ── Success — owner ───────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_Owner_DeletesAndLogsMemberLeft()
    {
        SetupCharacter();
        _memberships.Setup(r => r.DeleteAsync(CharacterId, GuildId, default)).ReturnsAsync(true);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _memberships.Verify(r => r.DeleteAsync(CharacterId, GuildId, default), Times.Once);
        _auditLog.Verify(a => a.LogAsync(
            GuildId, DiscordId, GuildAuditAction.MemberLeft,
            It.Is<Dictionary<string, string>>(v => v["characterName"] == "Arthas" && v["characterClassId"] == "5"),
            default), Times.Once);
        _guildAccess.Verify(a => a.GetAccessLevelAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    // ── Success — officer kicking someone else ───────────────────────────

    [Fact]
    public async Task HandleAsync_OfficerNotOwner_DeletesAndLogsMemberExcluded()
    {
        _characters.Setup(r => r.GetByIdAsync(CharacterId, default))
            .ReturnsAsync(new Character { Id = CharacterId, Name = "Arthas", UserDiscordId = OwnerId, ClassId = 5 });
        _guildAccess.Setup(a => a.GetAccessLevelAsync(DiscordId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _memberships.Setup(r => r.DeleteAsync(CharacterId, GuildId, default)).ReturnsAsync(true);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _memberships.Verify(r => r.DeleteAsync(CharacterId, GuildId, default), Times.Once);
        _auditLog.Verify(a => a.LogAsync(
            GuildId, DiscordId, GuildAuditAction.MemberExcluded,
            It.Is<Dictionary<string, string>>(v => v["characterName"] == "Arthas" && v["characterClassId"] == "5"),
            default), Times.Once);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void SetupCharacter() =>
        _characters.Setup(r => r.GetByIdAsync(CharacterId, default))
            .ReturnsAsync(new Character { Id = CharacterId, Name = "Arthas", UserDiscordId = DiscordId, ClassId = 5 });
}
