using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Services;

public class RaidSlotEligibilityValidatorTests
{
    private readonly Mock<IGuildMembershipRepository> _guildMembershipRepository = new();
    private readonly Mock<IRaidAvailabilityService> _raidAvailabilityService = new();
    private readonly Mock<IRaidSignupRepository> _raidSignupRepository = new();
    private readonly Mock<IRaidLockoutConflictChecker> _raidLockoutConflictChecker = new();
    private readonly RaidSlotEligibilityValidator _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const int CharacterId = 42;

    public RaidSlotEligibilityValidatorTests()
    {
        _sut = new RaidSlotEligibilityValidator(_guildMembershipRepository.Object, _raidAvailabilityService.Object, _raidSignupRepository.Object, _raidLockoutConflictChecker.Object);
    }

    // ── ValidateRosterMembershipAsync ─────────────────────────────────────────

    [Fact]
    public async Task ValidateRosterMembershipAsync_NoMembershipOnThisGuildBranch_ReturnsCharacterNotOnRoster()
    {
        _guildMembershipRepository.Setup(r => r.GetByCharacterIdAsync(CharacterId, default)).ReturnsAsync(
        [
            new GuildMembership { CharacterId = CharacterId, GuildBranchId = GuildBranchId + 1 },
        ]);

        var result = await _sut.ValidateRosterMembershipAsync(CharacterId, GuildBranchId);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.CharacterNotOnRoster);
    }

    [Fact]
    public async Task ValidateRosterMembershipAsync_NoMembershipsAtAll_ReturnsCharacterNotOnRoster()
    {
        _guildMembershipRepository.Setup(r => r.GetByCharacterIdAsync(CharacterId, default)).ReturnsAsync([]);

        var result = await _sut.ValidateRosterMembershipAsync(CharacterId, GuildBranchId);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.CharacterNotOnRoster);
    }

    [Fact]
    public async Task ValidateRosterMembershipAsync_HasMembershipOnThisGuildBranch_ReturnsOk()
    {
        _guildMembershipRepository.Setup(r => r.GetByCharacterIdAsync(CharacterId, default)).ReturnsAsync(
        [
            new GuildMembership { CharacterId = CharacterId, GuildBranchId = GuildBranchId },
        ]);

        var result = await _sut.ValidateRosterMembershipAsync(CharacterId, GuildBranchId);

        result.IsSuccess.Should().BeTrue();
    }

    // ── ValidateAssignabilityAsync ────────────────────────────────────────────

    private static RaidEvent MakeEvent() => new() { Id = 5, StartsAtUtc = new DateTime(2026, 2, 1, 20, 0, 0, DateTimeKind.Utc) };
    private static Character MakeCharacter() => new() { Id = CharacterId, UserDiscordId = "player-1" };

    [Fact]
    public async Task ValidateAssignabilityAsync_PlayerDeclaredUnavailable_ReturnsMemberDeclaredAbsent()
    {
        _raidAvailabilityService.Setup(s => s.IsPlayerUnavailableAsync("player-1", GuildId, GuildBranchId, It.IsAny<DateTime>(), default)).ReturnsAsync(true);

        var result = await _sut.ValidateAssignabilityAsync(MakeEvent(), MakeCharacter(), GuildId, GuildBranchId);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.MemberDeclaredAbsent);
        _raidLockoutConflictChecker.Verify(c => c.FindConflictingZoneNameAsync(It.IsAny<RaidEvent>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task ValidateAssignabilityAsync_LockoutConflictDetected_ReturnsRaidLockoutConflictWithZoneName()
    {
        _raidAvailabilityService.Setup(s => s.IsPlayerUnavailableAsync("player-1", GuildId, GuildBranchId, It.IsAny<DateTime>(), default)).ReturnsAsync(false);
        _raidLockoutConflictChecker.Setup(c => c.FindConflictingZoneNameAsync(It.IsAny<RaidEvent>(), CharacterId, GuildId, GuildBranchId, default)).ReturnsAsync("Zul'Gurub");

        var result = await _sut.ValidateAssignabilityAsync(MakeEvent(), MakeCharacter(), GuildId, GuildBranchId);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidLockoutConflict);
        result.Detail.Should().Contain("Zul'Gurub");
    }

    [Fact]
    public async Task ValidateAssignabilityAsync_AvailableAndNoLockoutConflict_ReturnsOk()
    {
        _raidAvailabilityService.Setup(s => s.IsPlayerUnavailableAsync("player-1", GuildId, GuildBranchId, It.IsAny<DateTime>(), default)).ReturnsAsync(false);
        _raidLockoutConflictChecker.Setup(c => c.FindConflictingZoneNameAsync(It.IsAny<RaidEvent>(), CharacterId, GuildId, GuildBranchId, default)).ReturnsAsync((string?)null);

        var result = await _sut.ValidateAssignabilityAsync(MakeEvent(), MakeCharacter(), GuildId, GuildBranchId);

        result.IsSuccess.Should().BeTrue();
    }

    // ── ValidateAssignabilityAsync — Signup mode ─────────────────────────────

    private static RaidEvent MakeSignupEvent() => new() { Id = 5, SignupMode = SignupMode.Signup, StartsAtUtc = new DateTime(2026, 2, 1, 20, 0, 0, DateTimeKind.Utc) };

    [Fact]
    public async Task ValidateAssignabilityAsync_SignupMode_NoResponseAtAll_ReturnsPlayerHasNotAcceptedSignup()
    {
        _raidSignupRepository.Setup(r => r.GetAsync(5, "player-1", default)).ReturnsAsync((RaidSignup?)null);

        var result = await _sut.ValidateAssignabilityAsync(MakeSignupEvent(), MakeCharacter(), GuildId, GuildBranchId);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.PlayerHasNotAcceptedSignup);
        _raidAvailabilityService.Verify(s => s.IsPlayerUnavailableAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<DateTime>(), default), Times.Never);
    }

    [Fact]
    public async Task ValidateAssignabilityAsync_SignupMode_TentativeNotAccepted_ReturnsPlayerHasNotAcceptedSignup()
    {
        _raidSignupRepository.Setup(r => r.GetAsync(5, "player-1", default)).ReturnsAsync(new RaidSignup { Status = SignupStatus.Tentative, CharacterId = CharacterId });

        var result = await _sut.ValidateAssignabilityAsync(MakeSignupEvent(), MakeCharacter(), GuildId, GuildBranchId);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.PlayerHasNotAcceptedSignup);
    }

    [Fact]
    public async Task ValidateAssignabilityAsync_SignupMode_AcceptedWithADifferentCharacter_ReturnsPlayerHasNotAcceptedSignup()
    {
        _raidSignupRepository.Setup(r => r.GetAsync(5, "player-1", default)).ReturnsAsync(new RaidSignup { Status = SignupStatus.Accepted, CharacterId = CharacterId + 1 });

        var result = await _sut.ValidateAssignabilityAsync(MakeSignupEvent(), MakeCharacter(), GuildId, GuildBranchId);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.PlayerHasNotAcceptedSignup);
    }

    [Fact]
    public async Task ValidateAssignabilityAsync_SignupMode_AcceptedWithTheSameCharacter_ChecksLockoutInsteadOfAvailability()
    {
        _raidSignupRepository.Setup(r => r.GetAsync(5, "player-1", default)).ReturnsAsync(new RaidSignup { Status = SignupStatus.Accepted, CharacterId = CharacterId });
        _raidLockoutConflictChecker.Setup(c => c.FindConflictingZoneNameAsync(It.IsAny<RaidEvent>(), CharacterId, GuildId, GuildBranchId, default)).ReturnsAsync((string?)null);

        var result = await _sut.ValidateAssignabilityAsync(MakeSignupEvent(), MakeCharacter(), GuildId, GuildBranchId);

        result.IsSuccess.Should().BeTrue();
        _raidAvailabilityService.Verify(s => s.IsPlayerUnavailableAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<DateTime>(), default), Times.Never);
    }
}
