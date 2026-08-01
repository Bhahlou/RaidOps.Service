using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Calendar.Availability.Responses;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Events.Queries;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Events.QueryHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Calendar;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Raids;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Events.QueryHandlers;

public class GetRaidBoardQueryHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IGuildsRepository> _guildsRepository = new();
    private readonly Mock<IRaidEventRepository> _raidEventRepository = new();
    private readonly Mock<IGuildMembershipRepository> _guildMembershipRepository = new();
    private readonly Mock<IAvailabilityRepository> _availabilityRepository = new();
    private readonly Mock<IAvailabilityResolutionService> _availabilityResolutionService = new();
    private readonly Mock<IUsersRepository> _usersRepository = new();
    private readonly Mock<ICharacterRepository> _characterRepository = new();
    private readonly GetRaidBoardQueryHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "roster-1";
    private const int CharacterId = 42;
    private const string AssignedPlayerId = "player-1";

    private static readonly DateTime EventStartsAtUtc = new(2026, 2, 4, 20, 0, 0, DateTimeKind.Utc);

    public GetRaidBoardQueryHandlerTests()
    {
        _sut = new GetRaidBoardQueryHandler(
            _access.Object, _guildsRepository.Object, _raidEventRepository.Object, _guildMembershipRepository.Object,
            _availabilityRepository.Object, _availabilityResolutionService.Object, _usersRepository.Object, _characterRepository.Object);

        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _guildsRepository.Setup(r => r.GetByIdAsync(GuildId, default)).ReturnsAsync(new Guild { Id = GuildId, Timezone = null });
        _guildMembershipRepository.Setup(r => r.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync([]);
        _availabilityRepository.Setup(r => r.GetExceptionsOverlappingForUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), default)).ReturnsAsync([]);
        _availabilityRepository.Setup(r => r.GetPatternsForUsersAsync(It.IsAny<IEnumerable<string>>(), default)).ReturnsAsync([]);
        _usersRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>(), default)).ReturnsAsync([]);
        _characterRepository.Setup(r => r.GetRaidSpecsForCharactersAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([]);
        _availabilityResolutionService.Setup(s => s.ResolveForScope(
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<IReadOnlyCollection<AvailabilityDeclaration>>(),
                It.IsAny<IReadOnlyCollection<RecurringAvailabilityPattern>>(), It.IsAny<string?>(), It.IsAny<int?>()))
            .Returns([new ResolvedDayAvailabilityResponse { Status = DayAvailabilityStatus.Available }]);
    }

    private static GetRaidBoardQuery MakeQuery() => new()
    {
        GuildId = GuildId,
        GuildBranchId = GuildBranchId,
        RequesterDiscordId = RequesterId,
        RangeStart = new DateOnly(2026, 2, 1),
        RangeEnd = new DateOnly(2026, 2, 7),
    };

    private static WowClass Warrior => new() { Id = 1, Name = "Warrior", Color = "C79C6E" };

    private static Spec ArmsSpec => new() { Id = 1, Name = "Arms", IconUrl = "arms.png" };

    private static Character MakeAssignedCharacter() => new()
    {
        Id = CharacterId,
        Name = "Arthas",
        UserDiscordId = AssignedPlayerId,
        ClassId = 1,
        Class = Warrior,
    };

    private static RaidEvent MakeEvent(RaidPublicationStatus publicationStatus, List<RaidSlotAssignment>? assignments = null, List<RaidEventZone>? targetZones = null) => new()
    {
        Id = 100,
        GuildBranchId = GuildBranchId,
        GuildBranch = new GuildBranch { Id = GuildBranchId, GuildId = GuildId, BranchId = 1, Branch = new Branch { Id = 1, Name = "Classic Era" } },
        Name = "Split 1",
        StartsAtUtc = EventStartsAtUtc,
        GroupCount = 2,
        SlotsPerGroup = 5,
        SignupMode = SignupMode.DefaultPresent,
        Status = RaidEventStatus.Scheduled,
        PublicationStatus = publicationStatus,
        TargetZones = targetZones ?? [new RaidEventZone { RaidZoneId = 7, RaidZone = new RaidZone { Id = 7, Name = "Molten Core", ShortCode = "MC" } }],
        Assignments = assignments ?? [],
    };

    private static RaidSlotAssignment MakeAssignment(Character character) => new()
    {
        GroupNumber = 1,
        SlotNumber = 1,
        CharacterId = character.Id,
        SpecId = 1,
        AssignedPlayerDiscordId = character.UserDiscordId,
        Character = character,
        Spec = ArmsSpec,
    };

    [Fact]
    public async Task HandleAsync_BelowRosterAccess_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Public);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_RangeEndBeforeRangeStart_ReturnsInvalidRequest()
    {
        var query = MakeQuery();
        query.RangeEnd = query.RangeStart.AddDays(-1);

        var result = await _sut.HandleAsync(query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidRequest);
    }

    [Fact]
    public async Task HandleAsync_GuildNotFound_ReturnsGuildNotFound()
    {
        _guildsRepository.Setup(r => r.GetByIdAsync(GuildId, default)).ReturnsAsync((Guild?)null);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildNotFound);
    }

    [Fact]
    public async Task HandleAsync_RosterAccess_OnlySeesPublishedEvents()
    {
        _raidEventRepository.Setup(r => r.GetForGuildBranchInRangeAsync(GuildBranchId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
            .ReturnsAsync([MakeEvent(RaidPublicationStatus.Draft), MakeEvent(RaidPublicationStatus.Published)]);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Events.Should().ContainSingle().Which.PublicationStatus.Should().Be(RaidPublicationStatus.Published);
    }

    [Fact]
    public async Task HandleAsync_OfficerAccess_SeesDraftAndPublishedEvents()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _raidEventRepository.Setup(r => r.GetForGuildBranchInRangeAsync(GuildBranchId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
            .ReturnsAsync([MakeEvent(RaidPublicationStatus.Draft), MakeEvent(RaidPublicationStatus.Published)]);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Events.Should().HaveCount(2);
    }

    [Fact]
    public async Task HandleAsync_MapsEventAndZoneFields()
    {
        _raidEventRepository.Setup(r => r.GetForGuildBranchInRangeAsync(GuildBranchId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
            .ReturnsAsync([MakeEvent(RaidPublicationStatus.Published)]);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        var ev = result.Value!.Events.Single();
        ev.Id.Should().Be(100);
        ev.BranchId.Should().Be(1);
        ev.BranchName.Should().Be("Classic Era");
        ev.RaidZones.Should().ContainSingle(z => z.Id == 7 && z.Name == "Molten Core" && z.ShortCode == "MC");
    }

    [Fact]
    public async Task HandleAsync_MapsAssignmentCharacterAndSpecFields()
    {
        var character = MakeAssignedCharacter();
        _raidEventRepository.Setup(r => r.GetForGuildBranchInRangeAsync(GuildBranchId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
            .ReturnsAsync([MakeEvent(RaidPublicationStatus.Published, assignments: [MakeAssignment(character)])]);
        _characterRepository.Setup(r => r.GetRaidSpecsForCharactersAsync(It.IsAny<IEnumerable<int>>(), default))
            .ReturnsAsync([
                new CharacterRaidSpec { CharacterId = CharacterId, SpecId = 1, IsMain = true, Spec = ArmsSpec },
                new CharacterRaidSpec { CharacterId = CharacterId, SpecId = 2, IsMain = false, Spec = new Spec { Id = 2, Name = "Fury" } },
            ]);
        _usersRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>(), default)).ReturnsAsync([new User { DiscordId = AssignedPlayerId, Name = "PlayerOne" }]);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        var assignment = result.Value!.Events.Single().Assignments.Single();
        assignment.CharacterId.Should().Be(CharacterId);
        assignment.CharacterName.Should().Be("Arthas");
        assignment.ClassId.Should().Be(1);
        assignment.ClassColor.Should().Be("#C79C6E");
        assignment.PlayerDiscordId.Should().Be(AssignedPlayerId);
        assignment.PlayerName.Should().Be("PlayerOne");
        assignment.Spec.Name.Should().Be("Arms");
        assignment.AvailableSpecs.Select(s => s.Name).Should().BeEquivalentTo(["Arms", "Fury"]);
    }

    [Fact]
    public async Task HandleAsync_UnresolvedPlayerName_IsNull()
    {
        var character = MakeAssignedCharacter();
        _raidEventRepository.Setup(r => r.GetForGuildBranchInRangeAsync(GuildBranchId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
            .ReturnsAsync([MakeEvent(RaidPublicationStatus.Published, assignments: [MakeAssignment(character)])]);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.Value!.Events.Single().Assignments.Single().PlayerName.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_AssignmentAvailabilityStatus_ReflectsResolvedStatus()
    {
        var character = MakeAssignedCharacter();
        _raidEventRepository.Setup(r => r.GetForGuildBranchInRangeAsync(GuildBranchId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
            .ReturnsAsync([MakeEvent(RaidPublicationStatus.Published, assignments: [MakeAssignment(character)])]);
        _availabilityResolutionService.Setup(s => s.ResolveForScope(
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<IReadOnlyCollection<AvailabilityDeclaration>>(),
                It.IsAny<IReadOnlyCollection<RecurringAvailabilityPattern>>(), It.IsAny<string?>(), It.IsAny<int?>()))
            .Returns([new ResolvedDayAvailabilityResponse { Status = DayAvailabilityStatus.Absent }]);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.Value!.Events.Single().Assignments.Single().AvailabilityStatus.Should().Be(DayAvailabilityStatus.Absent);
    }

    [Fact]
    public async Task HandleAsync_NoResolvedDay_AssignmentDefaultsToAvailable()
    {
        var character = MakeAssignedCharacter();
        _raidEventRepository.Setup(r => r.GetForGuildBranchInRangeAsync(GuildBranchId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
            .ReturnsAsync([MakeEvent(RaidPublicationStatus.Published, assignments: [MakeAssignment(character)])]);
        _availabilityResolutionService.Setup(s => s.ResolveForScope(
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<IReadOnlyCollection<AvailabilityDeclaration>>(),
                It.IsAny<IReadOnlyCollection<RecurringAvailabilityPattern>>(), It.IsAny<string?>(), It.IsAny<int?>()))
            .Returns([]);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.Value!.Events.Single().Assignments.Single().AvailabilityStatus.Should().Be(DayAvailabilityStatus.Available);
    }

    [Fact]
    public async Task HandleAsync_RosterPlayerDeclaredAbsent_AppearsInAbsentPlayerDiscordIds()
    {
        var absentCharacter = new Character { Id = 99, UserDiscordId = "player-absent", Class = Warrior };
        _guildMembershipRepository.Setup(r => r.GetByGuildBranchIdAsync(GuildBranchId, default))
            .ReturnsAsync([new GuildMembership { CharacterId = 99, GuildId = GuildId, GuildBranchId = GuildBranchId, Character = absentCharacter }]);
        _availabilityRepository.Setup(r => r.GetExceptionsOverlappingForUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), default))
            .ReturnsAsync([new AvailabilityDeclaration { UserDiscordId = "player-absent" }]);
        _availabilityResolutionService.Setup(s => s.ResolveForScope(
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.Is<IReadOnlyCollection<AvailabilityDeclaration>>(ex => ex.Any(e => e.UserDiscordId == "player-absent")),
                It.IsAny<IReadOnlyCollection<RecurringAvailabilityPattern>>(), It.IsAny<string?>(), It.IsAny<int?>()))
            .Returns([new ResolvedDayAvailabilityResponse { Status = DayAvailabilityStatus.Absent }]);
        _raidEventRepository.Setup(r => r.GetForGuildBranchInRangeAsync(GuildBranchId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
            .ReturnsAsync([MakeEvent(RaidPublicationStatus.Published)]);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.Value!.Events.Single().AbsentPlayerDiscordIds.Should().Contain("player-absent");
    }

    [Fact]
    public async Task HandleAsync_RosterPlayerPartialOutsideEventWindow_IsAbsent()
    {
        var character = new Character { Id = 99, UserDiscordId = "player-partial", Class = Warrior };
        _guildMembershipRepository.Setup(r => r.GetByGuildBranchIdAsync(GuildBranchId, default))
            .ReturnsAsync([new GuildMembership { CharacterId = 99, GuildId = GuildId, GuildBranchId = GuildBranchId, Character = character }]);
        _availabilityRepository.Setup(r => r.GetExceptionsOverlappingForUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), default))
            .ReturnsAsync([new AvailabilityDeclaration { UserDiscordId = "player-partial" }]);
        // Event starts 20:00 UTC; member only available until 18:00 — outside the window.
        _availabilityResolutionService.Setup(s => s.ResolveForScope(
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.Is<IReadOnlyCollection<AvailabilityDeclaration>>(ex => ex.Any(e => e.UserDiscordId == "player-partial")),
                It.IsAny<IReadOnlyCollection<RecurringAvailabilityPattern>>(), It.IsAny<string?>(), It.IsAny<int?>()))
            .Returns([new ResolvedDayAvailabilityResponse { Status = DayAvailabilityStatus.Partial, AvailableUntil = new TimeOnly(18, 0) }]);
        _raidEventRepository.Setup(r => r.GetForGuildBranchInRangeAsync(GuildBranchId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
            .ReturnsAsync([MakeEvent(RaidPublicationStatus.Published)]);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.Value!.Events.Single().AbsentPlayerDiscordIds.Should().Contain("player-partial");
    }

    [Fact]
    public async Task HandleAsync_RosterPlayerPartialWithinEventWindow_IsNotAbsent()
    {
        var character = new Character { Id = 99, UserDiscordId = "player-partial", Class = Warrior };
        _guildMembershipRepository.Setup(r => r.GetByGuildBranchIdAsync(GuildBranchId, default))
            .ReturnsAsync([new GuildMembership { CharacterId = 99, GuildId = GuildId, GuildBranchId = GuildBranchId, Character = character }]);
        _availabilityRepository.Setup(r => r.GetExceptionsOverlappingForUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), default))
            .ReturnsAsync([new AvailabilityDeclaration { UserDiscordId = "player-partial" }]);
        _availabilityResolutionService.Setup(s => s.ResolveForScope(
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.Is<IReadOnlyCollection<AvailabilityDeclaration>>(ex => ex.Any(e => e.UserDiscordId == "player-partial")),
                It.IsAny<IReadOnlyCollection<RecurringAvailabilityPattern>>(), It.IsAny<string?>(), It.IsAny<int?>()))
            .Returns([new ResolvedDayAvailabilityResponse { Status = DayAvailabilityStatus.Partial, AvailableFrom = new TimeOnly(18, 0), AvailableUntil = new TimeOnly(23, 0) }]);
        _raidEventRepository.Setup(r => r.GetForGuildBranchInRangeAsync(GuildBranchId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
            .ReturnsAsync([MakeEvent(RaidPublicationStatus.Published)]);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.Value!.Events.Single().AbsentPlayerDiscordIds.Should().NotContain("player-partial");
    }

    [Fact]
    public async Task HandleAsync_RosterPlayerAvailable_IsNotAbsent()
    {
        var character = new Character { Id = 99, UserDiscordId = "player-available", Class = Warrior };
        _guildMembershipRepository.Setup(r => r.GetByGuildBranchIdAsync(GuildBranchId, default))
            .ReturnsAsync([new GuildMembership { CharacterId = 99, GuildId = GuildId, GuildBranchId = GuildBranchId, Character = character }]);
        // Uses the constructor's default ResolveForScope stub, which resolves to Available.
        _raidEventRepository.Setup(r => r.GetForGuildBranchInRangeAsync(GuildBranchId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
            .ReturnsAsync([MakeEvent(RaidPublicationStatus.Published)]);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.Value!.Events.Single().AbsentPlayerDiscordIds.Should().NotContain("player-available");
    }

    [Fact]
    public async Task HandleAsync_NoResolvedDayForRosterPlayer_IsNotAbsent()
    {
        var character = new Character { Id = 99, UserDiscordId = "player-x", Class = Warrior };
        _guildMembershipRepository.Setup(r => r.GetByGuildBranchIdAsync(GuildBranchId, default))
            .ReturnsAsync([new GuildMembership { CharacterId = 99, GuildId = GuildId, GuildBranchId = GuildBranchId, Character = character }]);
        _availabilityResolutionService.Setup(s => s.ResolveForScope(
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<IReadOnlyCollection<AvailabilityDeclaration>>(),
                It.IsAny<IReadOnlyCollection<RecurringAvailabilityPattern>>(), It.IsAny<string?>(), It.IsAny<int?>()))
            .Returns([]);
        _raidEventRepository.Setup(r => r.GetForGuildBranchInRangeAsync(GuildBranchId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
            .ReturnsAsync([MakeEvent(RaidPublicationStatus.Published)]);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.Value!.Events.Single().AbsentPlayerDiscordIds.Should().BeEmpty();
    }
}
