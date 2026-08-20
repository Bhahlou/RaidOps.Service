using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Events.Queries;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Events.QueryHandlers;
using RaidOps.Domain.Enums;
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
    private readonly Mock<IRaidBoardEnrichmentDataLoader> _enrichmentDataLoader = new();
    private readonly Mock<IRaidAvailabilityLookup> _availabilityLookup = new();
    private readonly GetRaidBoardQueryHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "roster-1";
    private const int CharacterId = 42;
    private const string AssignedPlayerId = "player-1";

    private static readonly DateTime EventStartsAtUtc = new(2026, 2, 4, 20, 0, 0, DateTimeKind.Utc);

    public GetRaidBoardQueryHandlerTests()
    {
        _sut = new GetRaidBoardQueryHandler(_access.Object, _guildsRepository.Object, _raidEventRepository.Object, _guildMembershipRepository.Object, _enrichmentDataLoader.Object);

        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _guildsRepository.Setup(r => r.GetByIdAsync(GuildId, default)).ReturnsAsync(new Guild { Id = GuildId, Timezone = null });
        _guildMembershipRepository.Setup(r => r.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync([]);

        _availabilityLookup.Setup(l => l.ResolveStatus(It.IsAny<string>(), It.IsAny<DateOnly>())).Returns(DayAvailabilityStatus.Available);
        _availabilityLookup.Setup(l => l.IsUnavailableAt(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<TimeOnly>())).Returns(false);
        SetEnrichment();
    }

    /// <summary>(Re)stubs the loader's response — call again with overrides for tests that need specific enrichment data.</summary>
    private void SetEnrichment(
        Dictionary<string, User>? playersById = null,
        Dictionary<int, List<CharacterRaidSpec>>? raidSpecsByCharacter = null,
        Dictionary<int, Dictionary<string, RaidSignup>>? signupsByEvent = null) =>
        _enrichmentDataLoader.Setup(l => l.LoadAsync(It.IsAny<List<RaidEvent>>(), It.IsAny<List<string>>(), GuildId, GuildBranchId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), default))
            .ReturnsAsync(new RaidBoardEnrichmentData(playersById ?? [], _availabilityLookup.Object, signupsByEvent ?? [], raidSpecsByCharacter ?? []));

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

    private static RaidEvent MakeEvent(RaidPublicationStatus publicationStatus, List<RaidSlotAssignment>? assignments = null, List<RaidEventZone>? targetZones = null, SignupMode signupMode = SignupMode.DefaultPresent) => new()
    {
        Id = 100,
        GuildBranchId = GuildBranchId,
        GuildBranch = new GuildBranch { Id = GuildBranchId, GuildId = GuildId, BranchId = 1, Branch = new Branch { Id = 1, Name = "Classic Era" } },
        Name = "Split 1",
        StartsAtUtc = EventStartsAtUtc,
        GroupCount = 2,
        SlotsPerGroup = 5,
        SignupMode = signupMode,
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
    public async Task HandleAsync_MapsDedicatedAnnouncementChannelFields()
    {
        var raidEvent = MakeEvent(RaidPublicationStatus.Published);
        raidEvent.DedicatedAnnouncementChannelId = "999";
        raidEvent.DedicatedAnnouncementChannelIsBotOwned = true;
        _raidEventRepository.Setup(r => r.GetForGuildBranchInRangeAsync(GuildBranchId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
            .ReturnsAsync([raidEvent]);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        var ev = result.Value!.Events.Single();
        ev.DedicatedAnnouncementChannelId.Should().Be("999");
        ev.DedicatedAnnouncementChannelIsBotOwned.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_MapsAssignmentCharacterAndSpecFields()
    {
        var character = MakeAssignedCharacter();
        _raidEventRepository.Setup(r => r.GetForGuildBranchInRangeAsync(GuildBranchId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
            .ReturnsAsync([MakeEvent(RaidPublicationStatus.Published, assignments: [MakeAssignment(character)])]);
        SetEnrichment(
            playersById: new Dictionary<string, User> { [AssignedPlayerId] = new() { DiscordId = AssignedPlayerId, Name = "PlayerOne" } },
            raidSpecsByCharacter: new Dictionary<int, List<CharacterRaidSpec>>
            {
                [CharacterId] =
                [
                    new CharacterRaidSpec { CharacterId = CharacterId, SpecId = 1, IsMain = true, Spec = ArmsSpec },
                    new CharacterRaidSpec { CharacterId = CharacterId, SpecId = 2, IsMain = false, Spec = new Spec { Id = 2, Name = "Fury" } },
                ],
            });

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
    public async Task HandleAsync_AssignedPlayerHasRespondedToTheSignupCall_MapsAssignmentSignupStatus()
    {
        var character = MakeAssignedCharacter();
        var raidEvent = MakeEvent(RaidPublicationStatus.Published, assignments: [MakeAssignment(character)], signupMode: SignupMode.Signup);
        _raidEventRepository.Setup(r => r.GetForGuildBranchInRangeAsync(GuildBranchId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
            .ReturnsAsync([raidEvent]);
        SetEnrichment(signupsByEvent: new Dictionary<int, Dictionary<string, RaidSignup>>
        {
            [raidEvent.Id] = new()
            {
                [AssignedPlayerId] = new RaidSignup { RaidEventId = raidEvent.Id, UserDiscordId = AssignedPlayerId, Status = SignupStatus.Accepted, CharacterId = CharacterId },
            },
        });

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.Value!.Events.Single().Assignments.Single().SignupStatus.Should().Be(SignupStatus.Accepted);
    }

    [Fact]
    public async Task HandleAsync_AssignedPlayerHasNotRespondedToTheSignupCall_AssignmentSignupStatusIsNull()
    {
        var character = MakeAssignedCharacter();
        _raidEventRepository.Setup(r => r.GetForGuildBranchInRangeAsync(GuildBranchId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
            .ReturnsAsync([MakeEvent(RaidPublicationStatus.Published, assignments: [MakeAssignment(character)], signupMode: SignupMode.Signup)]);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.Value!.Events.Single().Assignments.Single().SignupStatus.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_AssignmentAvailabilityStatus_ReflectsLookupResolvedStatus()
    {
        var character = MakeAssignedCharacter();
        _raidEventRepository.Setup(r => r.GetForGuildBranchInRangeAsync(GuildBranchId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
            .ReturnsAsync([MakeEvent(RaidPublicationStatus.Published, assignments: [MakeAssignment(character)])]);
        _availabilityLookup.Setup(l => l.ResolveStatus(AssignedPlayerId, It.IsAny<DateOnly>())).Returns(DayAvailabilityStatus.Absent);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.Value!.Events.Single().Assignments.Single().AvailabilityStatus.Should().Be(DayAvailabilityStatus.Absent);
    }

    [Fact]
    public async Task HandleAsync_RosterPlayerLookupReportsUnavailable_AppearsInIneligiblePlayerDiscordIds()
    {
        var character = new Character { Id = 99, UserDiscordId = "player-absent", Class = Warrior };
        _guildMembershipRepository.Setup(r => r.GetByGuildBranchIdAsync(GuildBranchId, default))
            .ReturnsAsync([new GuildMembership { CharacterId = 99, GuildId = GuildId, GuildBranchId = GuildBranchId, Character = character }]);
        _availabilityLookup.Setup(l => l.IsUnavailableAt("player-absent", It.IsAny<DateOnly>(), It.IsAny<TimeOnly>())).Returns(true);
        _raidEventRepository.Setup(r => r.GetForGuildBranchInRangeAsync(GuildBranchId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
            .ReturnsAsync([MakeEvent(RaidPublicationStatus.Published)]);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.Value!.Events.Single().IneligiblePlayerDiscordIds.Should().Contain("player-absent");
    }

    [Fact]
    public async Task HandleAsync_RosterPlayerLookupReportsAvailable_DoesNotAppearInIneligiblePlayerDiscordIds()
    {
        var character = new Character { Id = 99, UserDiscordId = "player-available", Class = Warrior };
        _guildMembershipRepository.Setup(r => r.GetByGuildBranchIdAsync(GuildBranchId, default))
            .ReturnsAsync([new GuildMembership { CharacterId = 99, GuildId = GuildId, GuildBranchId = GuildBranchId, Character = character }]);
        _raidEventRepository.Setup(r => r.GetForGuildBranchInRangeAsync(GuildBranchId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
            .ReturnsAsync([MakeEvent(RaidPublicationStatus.Published)]);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.Value!.Events.Single().IneligiblePlayerDiscordIds.Should().NotContain("player-available");
    }

    [Fact]
    public async Task HandleAsync_PassesWholeRosterAndRangeToEnrichmentLoader()
    {
        var characterA = new Character { Id = 98, UserDiscordId = "player-a", Class = Warrior };
        var characterB = new Character { Id = 99, UserDiscordId = "player-b", Class = Warrior };
        _guildMembershipRepository.Setup(r => r.GetByGuildBranchIdAsync(GuildBranchId, default))
            .ReturnsAsync([
                new GuildMembership { CharacterId = 98, GuildId = GuildId, GuildBranchId = GuildBranchId, Character = characterA },
                new GuildMembership { CharacterId = 99, GuildId = GuildId, GuildBranchId = GuildBranchId, Character = characterB },
            ]);
        _raidEventRepository.Setup(r => r.GetForGuildBranchInRangeAsync(GuildBranchId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
            .ReturnsAsync([MakeEvent(RaidPublicationStatus.Published), MakeEvent(RaidPublicationStatus.Published)]);
        var query = MakeQuery();

        await _sut.HandleAsync(query, default);

        _enrichmentDataLoader.Verify(l => l.LoadAsync(
            It.Is<List<RaidEvent>>(events => events.Count == 2),
            It.Is<List<string>>(ids => ids.Contains("player-a") && ids.Contains("player-b")),
            GuildId, GuildBranchId, query.RangeStart, query.RangeEnd, default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_SignupModeEvent_IneligiblePlayersAreThoseWithoutAnAcceptedSignup()
    {
        var raidEvent = MakeEvent(RaidPublicationStatus.Published, signupMode: SignupMode.Signup);
        var characterA = new Character { Id = 98, UserDiscordId = "player-a", Class = Warrior };
        var characterB = new Character { Id = 99, UserDiscordId = "player-b", Class = Warrior };
        var characterC = new Character { Id = 97, UserDiscordId = "player-c", Class = Warrior };
        _guildMembershipRepository.Setup(r => r.GetByGuildBranchIdAsync(GuildBranchId, default))
            .ReturnsAsync([
                new GuildMembership { CharacterId = 98, GuildId = GuildId, GuildBranchId = GuildBranchId, Character = characterA },
                new GuildMembership { CharacterId = 99, GuildId = GuildId, GuildBranchId = GuildBranchId, Character = characterB },
                new GuildMembership { CharacterId = 97, GuildId = GuildId, GuildBranchId = GuildBranchId, Character = characterC },
            ]);
        _raidEventRepository.Setup(r => r.GetForGuildBranchInRangeAsync(GuildBranchId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
            .ReturnsAsync([raidEvent]);
        SetEnrichment(signupsByEvent: new Dictionary<int, Dictionary<string, RaidSignup>>
        {
            [raidEvent.Id] = new()
            {
                ["player-a"] = new RaidSignup { RaidEventId = raidEvent.Id, UserDiscordId = "player-a", Status = SignupStatus.Accepted, CharacterId = 98 },
                ["player-b"] = new RaidSignup { RaidEventId = raidEvent.Id, UserDiscordId = "player-b", Status = SignupStatus.Declined },
                // player-c has no entry at all — hasn't responded to the signup call yet.
            },
        });

        var result = await _sut.HandleAsync(MakeQuery(), default);

        var ev = result.Value!.Events.Single();
        ev.IneligiblePlayerDiscordIds.Should().Contain("player-b");
        ev.IneligiblePlayerDiscordIds.Should().Contain("player-c");
        ev.IneligiblePlayerDiscordIds.Should().NotContain("player-a");
    }

    [Fact]
    public async Task HandleAsync_SignupModeEvent_MapsAcceptedCharacterIdsByPlayerDiscordId()
    {
        var raidEvent = MakeEvent(RaidPublicationStatus.Published, signupMode: SignupMode.Signup);
        _raidEventRepository.Setup(r => r.GetForGuildBranchInRangeAsync(GuildBranchId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
            .ReturnsAsync([raidEvent]);
        SetEnrichment(signupsByEvent: new Dictionary<int, Dictionary<string, RaidSignup>>
        {
            [raidEvent.Id] = new()
            {
                ["player-a"] = new RaidSignup { RaidEventId = raidEvent.Id, UserDiscordId = "player-a", Status = SignupStatus.Accepted, CharacterId = 98 },
                ["player-b"] = new RaidSignup { RaidEventId = raidEvent.Id, UserDiscordId = "player-b", Status = SignupStatus.Declined, CharacterId = null },
                ["player-c"] = new RaidSignup { RaidEventId = raidEvent.Id, UserDiscordId = "player-c", Status = SignupStatus.Tentative, CharacterId = null },
            },
        });

        var result = await _sut.HandleAsync(MakeQuery(), default);

        var ev = result.Value!.Events.Single();
        ev.AcceptedCharacterIdsByPlayerDiscordId.Should().ContainSingle();
        ev.AcceptedCharacterIdsByPlayerDiscordId["player-a"].Should().Be(98);
    }
}
