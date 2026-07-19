using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RaidOps.Application.Contracts.Characters.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Characters.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Characters.CommandHandlers;

public class UnlinkBnetAccountCommandHandlerTests
{
    private readonly Mock<IBnetAccountRepository>     _bnetAccounts = new();
    private readonly Mock<ICharacterRepository>        _characters   = new();
    private readonly Mock<IGuildMembershipRepository>  _memberships  = new();
    private readonly Mock<IAuditLogService>             _auditLog     = new();
    private readonly UnlinkBnetAccountCommandHandler    _sut;

    private const string DiscordId = "user-1";
    private const string BnetId    = "bnet-1";

    private static readonly UnlinkBnetAccountCommand Command = new()
    {
        UserDiscordId = DiscordId,
        BnetId        = BnetId,
    };

    public UnlinkBnetAccountCommandHandlerTests()
    {
        _sut = new UnlinkBnetAccountCommandHandler(
            _bnetAccounts.Object,
            _characters.Object,
            _memberships.Object,
            _auditLog.Object,
            NullLogger<UnlinkBnetAccountCommandHandler>.Instance);

        _memberships.Setup(r => r.GetByCharacterIdsAsync(It.IsAny<IEnumerable<int>>(), default))
            .ReturnsAsync([]);
    }

    // ── No characters sourced from this account ─────────────────────────────

    [Fact]
    public async Task HandleAsync_NoCharactersFromAccount_DeletesAccountWithoutLoggingAudit()
    {
        _characters.Setup(r => r.GetByUserWithDetailsAsync(DiscordId, false, default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _auditLog.Verify(a => a.LogAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GuildAuditAction>(), It.IsAny<Dictionary<string, string>>(), default),
            Times.Never);
        _bnetAccounts.Verify(r => r.DeleteAsync(DiscordId, BnetId, default), Times.Once);
    }

    // ── Filtering by SourceBnetId ────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_OnlyConsidersCharactersSourcedFromTheGivenAccount()
    {
        var matching    = MakeCharacter(1, sourceBnetId: BnetId);
        var nonMatching = MakeCharacter(2, sourceBnetId: "other-bnet-id");
        _characters.Setup(r => r.GetByUserWithDetailsAsync(DiscordId, false, default))
            .ReturnsAsync([matching, nonMatching]);
        _memberships.Setup(r => r.GetByCharacterIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Single() == 1), default))
            .ReturnsAsync([]);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _memberships.Verify(r => r.GetByCharacterIdsAsync(
            It.Is<IEnumerable<int>>(ids => ids.Single() == 1), default), Times.Once);
    }

    // ── Audit logging per affected guild ─────────────────────────────────────

    [Fact]
    public async Task HandleAsync_CharacterInOneGuild_LogsOneMemberLeftAuditEntry()
    {
        var character = MakeCharacter(1, sourceBnetId: BnetId, name: "Arthas", classId: 6);
        _characters.Setup(r => r.GetByUserWithDetailsAsync(DiscordId, false, default)).ReturnsAsync([character]);
        _memberships.Setup(r => r.GetByCharacterIdsAsync(It.IsAny<IEnumerable<int>>(), default))
            .ReturnsAsync([new GuildMembership { CharacterId = 1, GuildId = "guild-1" }]);

        await _sut.HandleAsync(Command);

        _auditLog.Verify(a => a.LogAsync(
            "guild-1",
            DiscordId,
            GuildAuditAction.MemberLeft,
            It.Is<Dictionary<string, string>>(v => v["characterName"] == "Arthas" && v["characterClassId"] == "6"),
            default),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_CharacterInMultipleGuilds_LogsOneAuditEntryPerGuild()
    {
        var character = MakeCharacter(1, sourceBnetId: BnetId);
        _characters.Setup(r => r.GetByUserWithDetailsAsync(DiscordId, false, default)).ReturnsAsync([character]);
        _memberships.Setup(r => r.GetByCharacterIdsAsync(It.IsAny<IEnumerable<int>>(), default))
            .ReturnsAsync(
            [
                new GuildMembership { CharacterId = 1, GuildId = "guild-1" },
                new GuildMembership { CharacterId = 1, GuildId = "guild-2" },
            ]);

        await _sut.HandleAsync(Command);

        _auditLog.Verify(a => a.LogAsync(
            "guild-1", DiscordId, GuildAuditAction.MemberLeft, It.IsAny<Dictionary<string, string>>(), default), Times.Once);
        _auditLog.Verify(a => a.LogAsync(
            "guild-2", DiscordId, GuildAuditAction.MemberLeft, It.IsAny<Dictionary<string, string>>(), default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_CharacterFromAccountWithNoGuildMembership_DoesNotLogAudit()
    {
        var character = MakeCharacter(1, sourceBnetId: BnetId);
        _characters.Setup(r => r.GetByUserWithDetailsAsync(DiscordId, false, default)).ReturnsAsync([character]);
        _memberships.Setup(r => r.GetByCharacterIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _auditLog.Verify(a => a.LogAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GuildAuditAction>(), It.IsAny<Dictionary<string, string>>(), default),
            Times.Never);
        _bnetAccounts.Verify(r => r.DeleteAsync(DiscordId, BnetId, default), Times.Once);
    }

    // ── Ordering: audit entries must be written before the cascade delete ───

    [Fact]
    public async Task HandleAsync_LogsAuditEntriesBeforeDeletingTheAccount()
    {
        var character = MakeCharacter(1, sourceBnetId: BnetId);
        _characters.Setup(r => r.GetByUserWithDetailsAsync(DiscordId, false, default)).ReturnsAsync([character]);
        _memberships.Setup(r => r.GetByCharacterIdsAsync(It.IsAny<IEnumerable<int>>(), default))
            .ReturnsAsync([new GuildMembership { CharacterId = 1, GuildId = "guild-1" }]);

        var callOrder = new List<string>();
        _auditLog.Setup(a => a.LogAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GuildAuditAction>(), It.IsAny<Dictionary<string, string>>(), default))
            .Callback(() => callOrder.Add("audit"))
            .Returns(Task.CompletedTask);
        _bnetAccounts.Setup(r => r.DeleteAsync(DiscordId, BnetId, default))
            .Callback(() => callOrder.Add("delete"))
            .Returns(Task.CompletedTask);

        await _sut.HandleAsync(Command);

        callOrder.Should().Equal("audit", "delete");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Character MakeCharacter(int id, string sourceBnetId, string name = "Arthas", int classId = 6) => new()
    {
        Id              = id,
        Name            = name,
        UserDiscordId   = DiscordId,
        SourceBnetId    = sourceBnetId,
        ClassId         = classId,
        Faction         = Faction.Alliance,
        Branch          = new Branch { Id = 1, Name = "Retail", BnetNamespacePrefix = "dynamic", CurrentExpansionId = 10 },
        Realm           = new Realm  { Id = 1, Name = "Kazzak", Slug = "kazzak", Region = "eu", BranchId = 1 },
        ExpansionStates = [],
    };
}
