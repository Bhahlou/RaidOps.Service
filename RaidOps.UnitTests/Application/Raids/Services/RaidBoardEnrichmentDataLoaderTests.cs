using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Raids;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Services;

public class RaidBoardEnrichmentDataLoaderTests
{
    private readonly Mock<IRaidAvailabilityService> _raidAvailabilityService = new();
    private readonly Mock<IRaidAvailabilityLookup> _availabilityLookup = new();
    private readonly Mock<IRaidSignupRepository> _raidSignupRepository = new();
    private readonly Mock<IUsersRepository> _usersRepository = new();
    private readonly Mock<ICharacterRepository> _characterRepository = new();
    private readonly RaidBoardEnrichmentDataLoader _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const int CharacterId = 42;
    private const string AssignedPlayerId = "player-1";

    public RaidBoardEnrichmentDataLoaderTests()
    {
        _sut = new RaidBoardEnrichmentDataLoader(_raidAvailabilityService.Object, _raidSignupRepository.Object, _usersRepository.Object, _characterRepository.Object);

        _usersRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>(), default)).ReturnsAsync([]);
        _characterRepository.Setup(r => r.GetRaidSpecsForCharactersAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([]);
        _raidSignupRepository.Setup(r => r.GetForEventsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([]);
        _raidAvailabilityService.Setup(s => s.LoadRosterAvailabilityAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), default))
            .ReturnsAsync(_availabilityLookup.Object);
    }

    private static RaidEvent MakeEvent(int id, SignupMode signupMode = SignupMode.DefaultPresent, List<RaidSlotAssignment>? assignments = null) => new()
    {
        Id = id,
        GuildBranchId = GuildBranchId,
        Name = "Split 1",
        SignupMode = signupMode,
        Assignments = assignments ?? [],
    };

    [Fact]
    public async Task LoadAsync_PassesRosterPlayerIdsGuildAndRangeToAvailabilityService()
    {
        var rangeStart = new DateOnly(2026, 2, 1);
        var rangeEnd = new DateOnly(2026, 2, 7);

        await _sut.LoadAsync([MakeEvent(100)], ["player-a", "player-b"], GuildId, GuildBranchId, rangeStart, rangeEnd);

        _raidAvailabilityService.Verify(s => s.LoadRosterAvailabilityAsync(
            It.Is<IEnumerable<string>>(ids => ids.Contains("player-a") && ids.Contains("player-b")),
            GuildId, GuildBranchId, rangeStart, rangeEnd, default), Times.Once);
    }

    [Fact]
    public async Task LoadAsync_ReturnsAvailabilityLookupFromService()
    {
        var result = await _sut.LoadAsync([MakeEvent(100)], [], GuildId, GuildBranchId, default, default);

        result.AvailabilityLookup.Should().BeSameAs(_availabilityLookup.Object);
    }

    [Fact]
    public async Task LoadAsync_ResolvesPlayersById_FromAssignedPlayerIds()
    {
        var assignment = new RaidSlotAssignment { AssignedPlayerDiscordId = AssignedPlayerId, CharacterId = CharacterId };
        _usersRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>(), default))
            .ReturnsAsync([new User { DiscordId = AssignedPlayerId, Name = "PlayerOne" }]);

        var result = await _sut.LoadAsync([MakeEvent(100, assignments: [assignment])], [], GuildId, GuildBranchId, default, default);

        result.PlayersById.Should().ContainKey(AssignedPlayerId).WhoseValue.Name.Should().Be("PlayerOne");
    }

    [Fact]
    public async Task LoadAsync_GroupsRaidSpecsByCharacterId()
    {
        var assignment = new RaidSlotAssignment { AssignedPlayerDiscordId = AssignedPlayerId, CharacterId = CharacterId };
        _characterRepository.Setup(r => r.GetRaidSpecsForCharactersAsync(It.IsAny<IEnumerable<int>>(), default))
            .ReturnsAsync([
                new CharacterRaidSpec { CharacterId = CharacterId, SpecId = 1, Spec = new Spec { Id = 1, Name = "Arms" } },
                new CharacterRaidSpec { CharacterId = CharacterId, SpecId = 2, Spec = new Spec { Id = 2, Name = "Fury" } },
            ]);

        var result = await _sut.LoadAsync([MakeEvent(100, assignments: [assignment])], [], GuildId, GuildBranchId, default, default);

        result.RaidSpecsByCharacter.Should().ContainKey(CharacterId).WhoseValue.Select(s => s.Spec.Name).Should().BeEquivalentTo(["Arms", "Fury"]);
    }

    [Fact]
    public async Task LoadAsync_GroupsSignupsByEventThenPlayer_OnlyForSignupModeEvents()
    {
        var signupEvent = MakeEvent(100, signupMode: SignupMode.Signup);
        var defaultPresentEvent = MakeEvent(200, signupMode: SignupMode.DefaultPresent);
        _raidSignupRepository.Setup(r => r.GetForEventsAsync(It.Is<IEnumerable<int>>(ids => ids.Contains(100) && !ids.Contains(200)), default))
            .ReturnsAsync([new RaidSignup { RaidEventId = 100, UserDiscordId = AssignedPlayerId, Status = SignupStatus.Accepted }]);

        var result = await _sut.LoadAsync([signupEvent, defaultPresentEvent], [], GuildId, GuildBranchId, default, default);

        result.SignupsByEvent.Should().ContainKey(100).WhoseValue.Should().ContainKey(AssignedPlayerId)
            .WhoseValue.Status.Should().Be(SignupStatus.Accepted);
    }
}
