using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RaidOps.Application.Contracts.Common;
using DiscordGuild = RaidOps.Domain.Models.Discord.Guild;
using RaidOps.Application.Contracts.Guilds.Memberships.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Guilds.Memberships.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Discord;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Guilds.Memberships.CommandHandlers;

/// <summary>
/// Unit tests for <see cref="JoinGuildCommandHandler"/>. Branch/RosterMode/Discord-role
/// eligibility itself is covered by <c>GuildJoinEligibilityServiceTests</c> — this handler only
/// needs to prove it consults that service and reacts correctly to its outcome.
/// </summary>
public class JoinGuildCommandHandlerTests
{
    private readonly Mock<ICharacterRepository>          _characters   = new();
    private readonly Mock<IGuildsRepository>             _guilds       = new();
    private readonly Mock<IGuildJoinEligibilityService>  _eligibility  = new();
    private readonly Mock<IUserGuildsRepository>         _userGuilds   = new();
    private readonly Mock<IGuildMembershipRepository>    _memberships  = new();
    private readonly Mock<IAuditLogService>              _auditLog     = new();
    private readonly JoinGuildCommandHandler             _sut;

    private const int    CharacterId    = 1;
    private const string GuildId        = "guild-1";
    private const string DiscordId      = "200000000000000001";
    private const int    CharacterBranchId = 10;
    private const int    GuildBranchId  = 1;

    private static readonly JoinGuildCommand Command = new()
    {
        CharacterId        = CharacterId,
        GuildId            = GuildId,
        RequesterDiscordId = DiscordId,
        CharacterRank      = CharacterRank.Main,
    };

    public JoinGuildCommandHandlerTests()
    {
        _sut = new JoinGuildCommandHandler(
            _characters.Object,
            _guilds.Object,
            _eligibility.Object,
            _userGuilds.Object,
            _memberships.Object,
            _auditLog.Object,
            NullLogger<JoinGuildCommandHandler>.Instance);
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

    // ── GuildNotFound ─────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_GuildNotFound_ReturnsGuildNotFound()
    {
        SetupCharacter();
        _guilds.Setup(r => r.GetByIdAsync(GuildId, default)).ReturnsAsync((DiscordGuild?)null);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildNotFound);
    }

    // ── GuildNotRegistered ────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_GuildNotRegistered_ReturnsGuildNotRegistered()
    {
        SetupCharacter();
        _guilds.Setup(r => r.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild { Id = GuildId, Name = "RaidOps", IsRegistered = false });

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildNotRegistered);
    }

    // ── Eligibility check failure propagates as-is ────────────────────────

    [Fact]
    public async Task HandleAsync_NotEligibleForBranch_ReturnsTheEligibilityServiceError()
    {
        SetupCharacter();
        SetupRegisteredGuild();
        _eligibility
            .Setup(e => e.ResolveEligibleBranchAsync(GuildId, CharacterBranchId, DiscordId, default))
            .ReturnsAsync(Result<GuildBranch>.Fail(ResponseDetail.GuildBranchNotActive, "This guild does not run this character's WoW branch."));

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildBranchNotActive);
    }

    // ── NotDiscordMember ──────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_NotDiscordMember_ReturnsForbidden()
    {
        SetupCharacter();
        SetupRegisteredGuild();
        SetupEligibleBranch();
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    // ── AlreadyMember ─────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_AlreadyMember_ReturnsAlreadyMember()
    {
        SetupCharacter();
        SetupRegisteredGuild();
        SetupEligibleBranch();
        SetupDiscordMember();
        _memberships.Setup(r => r.ExistsAsync(CharacterId, GuildId, default)).ReturnsAsync(true);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.AlreadyMember);
    }

    // ── Success ───────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_Success_AddsAndLogs()
    {
        SetupCharacter();
        SetupRegisteredGuild();
        SetupEligibleBranch();
        SetupDiscordMember();
        _memberships.Setup(r => r.ExistsAsync(CharacterId, GuildId, default)).ReturnsAsync(false);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _memberships.Verify(r => r.AddAsync(
            It.Is<GuildMembership>(m => m.CharacterId == CharacterId && m.GuildId == GuildId &&
                                        m.GuildBranchId == GuildBranchId && m.CharacterRank == CharacterRank.Main),
            default), Times.Once);
        _auditLog.Verify(a => a.LogAsync(
            GuildId, DiscordId, GuildAuditAction.MemberJoined,
            It.Is<Dictionary<string, string>>(v => v["characterName"] == "Arthas" && v["characterClassId"] == "5"),
            default), Times.Once);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void SetupCharacter() =>
        _characters.Setup(r => r.GetByIdAsync(CharacterId, default))
            .ReturnsAsync(new Character { Id = CharacterId, Name = "Arthas", UserDiscordId = DiscordId, ClassId = 5, BranchId = CharacterBranchId });

    private void SetupRegisteredGuild() =>
        _guilds.Setup(r => r.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new DiscordGuild { Id = GuildId, Name = "RaidOps", IsRegistered = true });

    private void SetupEligibleBranch() =>
        _eligibility
            .Setup(e => e.ResolveEligibleBranchAsync(GuildId, CharacterBranchId, DiscordId, default))
            .ReturnsAsync(Result<GuildBranch>.Ok(new GuildBranch
            {
                Id = GuildBranchId,
                GuildId = GuildId,
                BranchId = CharacterBranchId,
                RosterMode = RosterMode.Open,
                IsActive = true,
            }));

    private void SetupDiscordMember() =>
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(DiscordId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = DiscordId }]);
}
