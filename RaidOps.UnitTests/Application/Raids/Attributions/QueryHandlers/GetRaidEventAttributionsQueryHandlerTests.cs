using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Attributions.Queries;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Attributions.QueryHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Raids;
using RaidOps.Domain.Models.Raids.Attributions;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Attributions.QueryHandlers;

/// <summary>
/// Unit tests for <see cref="GetRaidEventAttributionsQueryHandler"/>.
/// </summary>
public class GetRaidEventAttributionsQueryHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IRaidEventRepository> _raidEvents = new();
    private readonly Mock<IGuildAttributionDefinitionsRepository> _definitions = new();
    private readonly Mock<IRaidEventAttributionsRepository> _attributions = new();
    private readonly GetRaidEventAttributionsQueryHandler _sut;

    private const string GuildId = "guild-1";
    private const string RequesterId = "player-1";
    private const int GuildBranchId = 7;
    private const int EventId = 42;

    private static readonly GetRaidEventAttributionsQuery Query = new() { GuildId = GuildId, RequesterDiscordId = RequesterId, GuildBranchId = GuildBranchId, EventId = EventId };

    public GetRaidEventAttributionsQueryHandlerTests()
    {
        _sut = new GetRaidEventAttributionsQueryHandler(_access.Object, _raidEvents.Object, _definitions.Object, _attributions.Object);
        _definitions.Setup(d => d.GetForGuildAsync(GuildId, default)).ReturnsAsync([]);
        _attributions.Setup(a => a.GetForEventAsync(EventId, default)).ReturnsAsync([]);
    }

    [Fact]
    public async Task HandleAsync_BelowRoster_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Public);

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_RaidEventNotFound_ReturnsRaidEventNotFound()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _raidEvents.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync((RaidEvent?)null);

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidEventNotFound);
    }

    [Fact]
    public async Task HandleAsync_RosterMemberOnUnpublishedDraft_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _raidEvents.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent
        {
            Id = EventId, PublicationStatus = RaidPublicationStatus.Draft, SignupMode = SignupMode.DefaultPresent, Assignments = [],
        });

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_OfficerOnUnpublishedDraft_Succeeds()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _raidEvents.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent
        {
            Id = EventId, PublicationStatus = RaidPublicationStatus.Draft, SignupMode = SignupMode.DefaultPresent, Assignments = [],
        });

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_RosterMemberOnDraftWithSignupMode_Succeeds()
    {
        // A draft event in Signup mode is still visible to non-officers — the visibility check
        // is `Published || SignupMode == Signup`, an OR, not "must be published".
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _raidEvents.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent
        {
            Id = EventId, PublicationStatus = RaidPublicationStatus.Draft, SignupMode = SignupMode.Signup, Assignments = [],
        });

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_Success_MapsDefinitionsFillsAndSeatedCharacters()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _raidEvents.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent
        {
            Id = EventId,
            PublicationStatus = RaidPublicationStatus.Published,
            Assignments =
            [
                new RaidSlotAssignment { CharacterId = 100, SpecId = 265, Character = new Character { Id = 100, Name = "Aphrodisia", ClassId = 9 } },
            ],
        });
        _definitions.Setup(d => d.GetForGuildAsync(GuildId, default)).ReturnsAsync(
        [
            new GuildAttributionDefinition { Id = 1, GuildId = GuildId, Label = "Innervate", SortOrder = 0, Cells = [] },
        ]);
        _attributions.Setup(a => a.GetForEventAsync(EventId, default)).ReturnsAsync(
        [
            new RaidEventAttribution { RaidEventId = EventId, GuildAttributionDefinitionId = 1, AttributionDefinitionCellId = 10, InstanceIndex = 0, CharacterId = 100 },
        ]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Definitions.Should().ContainSingle(d => d.Id == 1 && d.Label == "Innervate");

        var fill = result.Value!.Fills.Should().ContainSingle().Subject;
        fill.DefinitionId.Should().Be(1);
        fill.CellId.Should().Be(10);
        fill.CharacterId.Should().Be(100);
        fill.CharacterName.Should().Be("Aphrodisia");
        fill.ClassId.Should().Be(9);

        var seated = result.Value!.SeatedCharacters.Should().ContainSingle().Subject;
        seated.CharacterId.Should().Be(100);
        seated.Name.Should().Be("Aphrodisia");
        seated.ClassId.Should().Be(9);
        seated.SpecId.Should().Be(265);
    }

    [Fact]
    public async Task HandleAsync_FillForNoLongerSeatedCharacter_IsDropped()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _raidEvents.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent
        {
            Id = EventId, PublicationStatus = RaidPublicationStatus.Published, Assignments = [],
        });
        _attributions.Setup(a => a.GetForEventAsync(EventId, default)).ReturnsAsync(
        [
            new RaidEventAttribution { RaidEventId = EventId, GuildAttributionDefinitionId = 1, AttributionDefinitionCellId = 10, InstanceIndex = 0, CharacterId = 999 },
        ]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Fills.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_MultipleAssignmentsForSameCharacter_DeduplicatesSeatedCharacters()
    {
        // A character could theoretically appear in more than one grid slot in bad data — the
        // handler's `DistinctBy(a => a.CharacterId)` keeps seated-character output stable either way.
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _raidEvents.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent
        {
            Id = EventId,
            PublicationStatus = RaidPublicationStatus.Published,
            Assignments =
            [
                new RaidSlotAssignment { CharacterId = 100, SpecId = 265, Character = new Character { Id = 100, Name = "Aphrodisia", ClassId = 9 } },
                new RaidSlotAssignment { CharacterId = 100, SpecId = 265, Character = new Character { Id = 100, Name = "Aphrodisia", ClassId = 9 } },
            ],
        });

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.SeatedCharacters.Should().ContainSingle();
    }
}
