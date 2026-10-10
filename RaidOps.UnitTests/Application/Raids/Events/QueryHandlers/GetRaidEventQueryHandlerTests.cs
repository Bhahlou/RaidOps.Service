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

public class GetRaidEventQueryHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IGuildsRepository> _guildsRepository = new();
    private readonly Mock<IRaidEventRepository> _raidEventRepository = new();
    private readonly Mock<IGuildMembershipRepository> _guildMembershipRepository = new();
    private readonly Mock<IRaidBoardEnrichmentDataLoader> _enrichmentDataLoader = new();
    private readonly Mock<IRaidAvailabilityLookup> _availabilityLookup = new();
    private readonly GetRaidEventQueryHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "roster-1";
    private const int EventId = 100;

    private static readonly DateTime EventStartsAtUtc = new(2026, 2, 4, 20, 0, 0, DateTimeKind.Utc);

    public GetRaidEventQueryHandlerTests()
    {
        _sut = new GetRaidEventQueryHandler(_access.Object, _guildsRepository.Object, _raidEventRepository.Object, _guildMembershipRepository.Object, _enrichmentDataLoader.Object);

        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _guildsRepository.Setup(r => r.GetByIdAsync(GuildId, default)).ReturnsAsync(new Guild { Id = GuildId, Timezone = null });
        _guildMembershipRepository.Setup(r => r.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync([]);
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(MakeEvent(RaidPublicationStatus.Published));

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

    private static GetRaidEventQuery MakeQuery() => new()
    {
        GuildId = GuildId,
        GuildBranchId = GuildBranchId,
        EventId = EventId,
        RequesterDiscordId = RequesterId,
    };

    private static RaidEvent MakeEvent(RaidPublicationStatus publicationStatus, SignupMode signupMode = SignupMode.DefaultPresent) => new()
    {
        Id = EventId,
        GuildBranchId = GuildBranchId,
        GuildBranch = new GuildBranch { Id = GuildBranchId, GuildId = GuildId, BranchId = 1, Branch = new Branch { Id = 1, Name = "Classic Era" } },
        Name = "Split 1",
        StartsAtUtc = EventStartsAtUtc,
        GroupCount = 2,
        SlotsPerGroup = 5,
        SignupMode = signupMode,
        Status = RaidEventStatus.Scheduled,
        PublicationStatus = publicationStatus,
        TargetZones = [new RaidEventZone { RaidZoneId = 7, RaidZone = new RaidZone { Id = 7, Name = "Molten Core", ShortCode = "MC" } }],
        Assignments = [],
    };

    [Fact]
    public async Task HandleAsync_BelowRosterAccess_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Public);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
        _raidEventRepository.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<int>(), default), Times.Never);
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
    public async Task HandleAsync_EventNotFound_ReturnsRaidEventNotFound()
    {
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync((RaidEvent?)null);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidEventNotFound);
    }

    [Fact]
    public async Task HandleAsync_RosterAccess_DraftDefaultPresentEvent_ReturnsForbidden()
    {
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(MakeEvent(RaidPublicationStatus.Draft));

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_RosterAccess_DraftSignupModeEvent_ReturnsEvent()
    {
        // The whole point of Signup mode is gathering responses before the raid is built — a
        // Roster requester must still see (and be able to respond to) a draft Signup event.
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default))
            .ReturnsAsync(MakeEvent(RaidPublicationStatus.Draft, SignupMode.Signup));

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_RosterAccess_PublishedEvent_ReturnsEvent()
    {
        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_OfficerAccess_DraftDefaultPresentEvent_ReturnsEvent()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(MakeEvent(RaidPublicationStatus.Draft));

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_MapsEventAndZoneFields()
    {
        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.Value!.Id.Should().Be(EventId);
        result.Value.Name.Should().Be("Split 1");
        result.Value.BranchId.Should().Be(1);
        result.Value.BranchName.Should().Be("Classic Era");
        result.Value.RaidZones.Should().ContainSingle(z => z.Id == 7 && z.Name == "Molten Core" && z.ShortCode == "MC");
    }

    [Fact]
    public async Task HandleAsync_SignupModeEventWithAResponse_MapsMySignupFields()
    {
        var raidEvent = MakeEvent(RaidPublicationStatus.Published, SignupMode.Signup);
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(raidEvent);
        SetEnrichment(signupsByEvent: new Dictionary<int, Dictionary<string, RaidSignup>>
        {
            [EventId] = new()
            {
                [RequesterId] = new RaidSignup { RaidEventId = EventId, UserDiscordId = RequesterId, Status = SignupStatus.Accepted, CharacterId = 42, SpecId = 71 },
            },
        });

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.Value!.MySignupStatus.Should().Be(SignupStatus.Accepted);
        result.Value.MySignupCharacterId.Should().Be(42);
        result.Value.MySignupSpecId.Should().Be(71);
    }

    [Fact]
    public async Task HandleAsync_PassesTheSingleEventAndWholeRosterToTheEnrichmentLoader()
    {
        var characterA = new Character { Id = 98, UserDiscordId = "player-a", Class = new WowClass { Id = 1, Name = "Warrior", Color = "C79C6E" } };
        _guildMembershipRepository.Setup(r => r.GetByGuildBranchIdAsync(GuildBranchId, default))
            .ReturnsAsync([new GuildMembership { CharacterId = 98, GuildId = GuildId, GuildBranchId = GuildBranchId, Character = characterA }]);

        await _sut.HandleAsync(MakeQuery(), default);

        var expectedLocalDate = DateOnly.FromDateTime(EventStartsAtUtc);
        _enrichmentDataLoader.Verify(l => l.LoadAsync(
            It.Is<List<RaidEvent>>(events => events.Count == 1 && events[0].Id == EventId),
            It.Is<List<string>>(ids => ids.Contains("player-a")),
            GuildId, GuildBranchId, expectedLocalDate, expectedLocalDate, default), Times.Once);
    }
}
