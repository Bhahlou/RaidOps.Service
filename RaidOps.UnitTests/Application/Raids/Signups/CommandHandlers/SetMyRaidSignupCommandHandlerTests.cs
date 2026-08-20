using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Signups.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Signups.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Signups.CommandHandlers;

public class SetMyRaidSignupCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IRaidEventRepository> _raidEventRepository = new();
    private readonly Mock<ICharacterRepository> _characterRepository = new();
    private readonly Mock<IGuildMembershipRepository> _guildMembershipRepository = new();
    private readonly Mock<IRaidSignupRepository> _raidSignupRepository = new();
    private readonly Mock<IRaidSlotUnassignmentService> _raidSlotUnassignmentService = new();
    private readonly Mock<IRaidSignupChangeNotifier> _raidSignupChangeNotifier = new();
    private readonly SetMyRaidSignupCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "player-1";
    private const int EventId = 5;
    private const int CharacterId = 42;
    private const int SpecId = 71;

    public SetMyRaidSignupCommandHandlerTests()
    {
        _sut = new SetMyRaidSignupCommandHandler(
            _access.Object, _raidEventRepository.Object, _characterRepository.Object, _guildMembershipRepository.Object,
            _raidSignupRepository.Object, _raidSlotUnassignmentService.Object, _raidSignupChangeNotifier.Object);
    }

    private static SetMyRaidSignupCommand MakeCommand(SignupStatus status = SignupStatus.Accepted, int? characterId = CharacterId, int? specId = SpecId) => new()
    {
        GuildId = GuildId,
        RequesterDiscordId = RequesterId,
        GuildBranchId = GuildBranchId,
        EventId = EventId,
        Status = status,
        CharacterId = characterId,
        SpecId = specId,
    };

    private void SetupRoster() =>
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);

    private void SetupSignupEvent(RaidEvent? raidEvent = null) =>
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default))
            .ReturnsAsync(raidEvent ?? new RaidEvent { Id = EventId, GuildBranchId = GuildBranchId, SignupMode = SignupMode.Signup, Assignments = [] });

    private void SetupValidCharacter()
    {
        _characterRepository.Setup(c => c.GetByIdAsync(CharacterId, default)).ReturnsAsync(new Character { Id = CharacterId, UserDiscordId = RequesterId });
        _guildMembershipRepository.Setup(m => m.GetByCharacterIdAsync(CharacterId, default))
            .ReturnsAsync([new GuildMembership { GuildBranchId = GuildBranchId }]);
        _characterRepository.Setup(c => c.GetRaidSpecsAsync(CharacterId, default)).ReturnsAsync([new CharacterRaidSpec { CharacterId = CharacterId, SpecId = SpecId }]);
    }

    // ── access / event guards ────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_BelowRosterAccess_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Public);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
        _raidSignupRepository.Verify(r => r.SetSignupAsync(It.IsAny<RaidSignup>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_EventNotFound_ReturnsRaidEventNotFound()
    {
        SetupRoster();
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync((RaidEvent?)null);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidEventNotFound);
    }

    [Fact]
    public async Task HandleAsync_EventNotInSignupMode_ReturnsRaidEventNotInSignupMode()
    {
        SetupRoster();
        SetupSignupEvent(new RaidEvent { Id = EventId, GuildBranchId = GuildBranchId, SignupMode = SignupMode.DefaultPresent, Assignments = [] });

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidEventNotInSignupMode);
    }

    [Fact]
    public async Task HandleAsync_DraftSignupEvent_StillSucceeds()
    {
        // No Published gate — signups must work while still Draft, that's the whole point of Signup mode.
        SetupRoster();
        SetupSignupEvent(new RaidEvent { Id = EventId, GuildBranchId = GuildBranchId, SignupMode = SignupMode.Signup, PublicationStatus = RaidPublicationStatus.Draft, Assignments = [] });
        SetupValidCharacter();

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
    }

    // ── character/spec resolution ────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_Declined_NeverResolvesCharacterOrSpec()
    {
        SetupRoster();
        SetupSignupEvent();

        var result = await _sut.HandleAsync(MakeCommand(status: SignupStatus.Declined, characterId: null, specId: null));

        result.IsSuccess.Should().BeTrue();
        _characterRepository.Verify(c => c.GetByIdAsync(It.IsAny<int>(), default), Times.Never);
        _raidSignupRepository.Verify(r => r.SetSignupAsync(It.Is<RaidSignup>(s => s.CharacterId == null && s.SpecId == null && s.Status == SignupStatus.Declined), default), Times.Once);
    }

    [Theory]
    [InlineData(SignupStatus.Accepted)]
    [InlineData(SignupStatus.Tentative)]
    public async Task HandleAsync_AcceptedOrTentativeWithoutCharacterId_ReturnsCharacterRequiredForSignup(SignupStatus status)
    {
        SetupRoster();
        SetupSignupEvent();

        var result = await _sut.HandleAsync(MakeCommand(status: status, characterId: null));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.CharacterRequiredForSignup);
    }

    [Fact]
    public async Task HandleAsync_CharacterNotFound_ReturnsCharacterNotFound()
    {
        SetupRoster();
        SetupSignupEvent();
        _characterRepository.Setup(c => c.GetByIdAsync(CharacterId, default)).ReturnsAsync((Character?)null);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.CharacterNotFound);
    }

    [Fact]
    public async Task HandleAsync_CharacterBelongsToSomeoneElse_ReturnsCharacterNotOwned()
    {
        SetupRoster();
        SetupSignupEvent();
        _characterRepository.Setup(c => c.GetByIdAsync(CharacterId, default)).ReturnsAsync(new Character { Id = CharacterId, UserDiscordId = "someone-else" });

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.CharacterNotOwned);
    }

    [Fact]
    public async Task HandleAsync_CharacterNotOnThisBranchRoster_ReturnsCharacterNotOnRoster()
    {
        SetupRoster();
        SetupSignupEvent();
        _characterRepository.Setup(c => c.GetByIdAsync(CharacterId, default)).ReturnsAsync(new Character { Id = CharacterId, UserDiscordId = RequesterId });
        _guildMembershipRepository.Setup(m => m.GetByCharacterIdAsync(CharacterId, default)).ReturnsAsync([new GuildMembership { GuildBranchId = GuildBranchId + 999 }]);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.CharacterNotOnRoster);
    }

    [Fact]
    public async Task HandleAsync_NoSpecId_ReturnsSpecRequiredForSignup()
    {
        SetupRoster();
        SetupSignupEvent();
        _characterRepository.Setup(c => c.GetByIdAsync(CharacterId, default)).ReturnsAsync(new Character { Id = CharacterId, UserDiscordId = RequesterId });
        _guildMembershipRepository.Setup(m => m.GetByCharacterIdAsync(CharacterId, default)).ReturnsAsync([new GuildMembership { GuildBranchId = GuildBranchId }]);

        var result = await _sut.HandleAsync(MakeCommand(specId: null));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.SpecRequiredForSignup);
    }

    [Fact]
    public async Task HandleAsync_SpecNotAmongCharactersDeclaredRaidSpecs_ReturnsSpecNotAvailableForCharacter()
    {
        SetupRoster();
        SetupSignupEvent();
        _characterRepository.Setup(c => c.GetByIdAsync(CharacterId, default)).ReturnsAsync(new Character { Id = CharacterId, UserDiscordId = RequesterId });
        _guildMembershipRepository.Setup(m => m.GetByCharacterIdAsync(CharacterId, default)).ReturnsAsync([new GuildMembership { GuildBranchId = GuildBranchId }]);
        _characterRepository.Setup(c => c.GetRaidSpecsAsync(CharacterId, default)).ReturnsAsync([new CharacterRaidSpec { CharacterId = CharacterId, SpecId = SpecId + 1 }]);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.SpecNotAvailableForCharacter);
    }

    [Fact]
    public async Task HandleAsync_ValidAcceptedResponse_SavesSignupWithCharacterAndSpec()
    {
        SetupRoster();
        SetupSignupEvent();
        SetupValidCharacter();

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _raidSignupRepository.Verify(r => r.SetSignupAsync(It.Is<RaidSignup>(s =>
            s.RaidEventId == EventId && s.UserDiscordId == RequesterId &&
            s.Status == SignupStatus.Accepted && s.CharacterId == CharacterId && s.SpecId == SpecId),
            default), Times.Once);
    }

    // ── stale-slot unassignment ──────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_NoExistingAssignments_NeverUnassignsAnything()
    {
        SetupRoster();
        SetupSignupEvent();
        SetupValidCharacter();

        await _sut.HandleAsync(MakeCommand());

        _raidSlotUnassignmentService.Verify(s => s.UnassignAsync(It.IsAny<RaidEvent>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_DeclinedWithAnExistingSlot_UnassignsIt()
    {
        SetupRoster();
        var raidEvent = new RaidEvent
        {
            Id = EventId,
            GuildBranchId = GuildBranchId,
            SignupMode = SignupMode.Signup,
            Assignments = [new RaidSlotAssignment { GroupNumber = 1, SlotNumber = 2, AssignedPlayerDiscordId = RequesterId, CharacterId = CharacterId }],
        };
        SetupSignupEvent(raidEvent);

        await _sut.HandleAsync(MakeCommand(status: SignupStatus.Declined, characterId: null, specId: null));

        _raidSlotUnassignmentService.Verify(s => s.UnassignAsync(raidEvent, 1, 2, RequesterId, default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_StillAcceptedWithSameCharacter_LeavesTheSlotAlone()
    {
        SetupRoster();
        var raidEvent = new RaidEvent
        {
            Id = EventId,
            GuildBranchId = GuildBranchId,
            SignupMode = SignupMode.Signup,
            Assignments = [new RaidSlotAssignment { GroupNumber = 1, SlotNumber = 2, AssignedPlayerDiscordId = RequesterId, CharacterId = CharacterId }],
        };
        SetupSignupEvent(raidEvent);
        SetupValidCharacter();

        await _sut.HandleAsync(MakeCommand());

        _raidSlotUnassignmentService.Verify(s => s.UnassignAsync(It.IsAny<RaidEvent>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_StillAcceptedButDifferentCharacter_UnassignsTheStaleSlot()
    {
        SetupRoster();
        const int oldCharacterId = 999;
        var raidEvent = new RaidEvent
        {
            Id = EventId,
            GuildBranchId = GuildBranchId,
            SignupMode = SignupMode.Signup,
            Assignments = [new RaidSlotAssignment { GroupNumber = 1, SlotNumber = 2, AssignedPlayerDiscordId = RequesterId, CharacterId = oldCharacterId }],
        };
        SetupSignupEvent(raidEvent);
        SetupValidCharacter();

        await _sut.HandleAsync(MakeCommand());

        _raidSlotUnassignmentService.Verify(s => s.UnassignAsync(raidEvent, 1, 2, RequesterId, default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AssignmentBelongsToSomeoneElse_NeverUnassignsIt()
    {
        SetupRoster();
        var raidEvent = new RaidEvent
        {
            Id = EventId,
            GuildBranchId = GuildBranchId,
            SignupMode = SignupMode.Signup,
            Assignments = [new RaidSlotAssignment { GroupNumber = 1, SlotNumber = 2, AssignedPlayerDiscordId = "someone-else", CharacterId = 999 }],
        };
        SetupSignupEvent(raidEvent);
        SetupValidCharacter();

        await _sut.HandleAsync(MakeCommand());

        _raidSlotUnassignmentService.Verify(s => s.UnassignAsync(It.IsAny<RaidEvent>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), default), Times.Never);
    }

    // ── notification ─────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_Success_NotifiesTheChange()
    {
        SetupRoster();
        SetupSignupEvent();
        SetupValidCharacter();

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _raidSignupChangeNotifier.Verify(n => n.NotifyChangedAsync(It.Is<RaidEvent>(e => e.Id == EventId), default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ValidationFailure_NeverNotifies()
    {
        SetupRoster();
        SetupSignupEvent();
        _characterRepository.Setup(c => c.GetByIdAsync(CharacterId, default)).ReturnsAsync((Character?)null);

        await _sut.HandleAsync(MakeCommand());

        _raidSignupChangeNotifier.Verify(n => n.NotifyChangedAsync(It.IsAny<RaidEvent>(), default), Times.Never);
    }
}
