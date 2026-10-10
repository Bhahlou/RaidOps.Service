using RaidOps.Domain.Models.Reference;
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
    private readonly Mock<IGuildBranchesRepository> _branches = new();
    private readonly Mock<IRaidEventRepository> _raidEvents = new();
    private readonly Mock<IGuildAttributionDefinitionsRepository> _definitions = new();
    private readonly Mock<IRaidEventAttributionsRepository> _attributions = new();
    private readonly Mock<IRaidBossRepository> _raidBosses = new();
    private readonly GetRaidEventAttributionsQueryHandler _sut;

    private const string GuildId = "guild-1";
    private const string RequesterId = "player-1";
    private const int GuildBranchId = 7;
    private const int EventId = 42;

    private static readonly GetRaidEventAttributionsQuery Query = new() { GuildId = GuildId, RequesterDiscordId = RequesterId, GuildBranchId = GuildBranchId, EventId = EventId };

    public GetRaidEventAttributionsQueryHandlerTests()
    {
        _sut = new GetRaidEventAttributionsQueryHandler(_access.Object, _branches.Object, _raidEvents.Object, _definitions.Object, _attributions.Object, _raidBosses.Object);
        _branches.Setup(b => b.GetCurrentExpansionIdAsync(GuildId, GuildBranchId, default)).ReturnsAsync(2);
        _definitions.Setup(d => d.GetForBranchAsync(GuildId, GuildBranchId, null, default)).ReturnsAsync([]);
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
        _definitions.Setup(d => d.GetForBranchAsync(GuildId, GuildBranchId, null, default)).ReturnsAsync(
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

    // ── BossId scope ─────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_BossIdGiven_BossDoesNotExist_ReturnsBossNotTargetedByEvent()
    {
        var query = new GetRaidEventAttributionsQuery { GuildId = GuildId, RequesterDiscordId = RequesterId, GuildBranchId = GuildBranchId, EventId = EventId, BossId = 14 };
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _raidEvents.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent { Id = EventId, PublicationStatus = RaidPublicationStatus.Published, Assignments = [] });
        _raidBosses.Setup(b => b.GetByIdAsync(14, default)).ReturnsAsync((RaidBoss?)null);

        var result = await _sut.HandleAsync(query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.BossNotTargetedByEvent);
    }

    [Fact]
    public async Task HandleAsync_BossIdGiven_ZoneNotTargetedByEvent_ReturnsBossNotTargetedByEvent()
    {
        var query = new GetRaidEventAttributionsQuery { GuildId = GuildId, RequesterDiscordId = RequesterId, GuildBranchId = GuildBranchId, EventId = EventId, BossId = 14 };
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _raidEvents.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent
        {
            Id = EventId, PublicationStatus = RaidPublicationStatus.Published, Assignments = [], TargetZones = [new RaidEventZone { RaidZoneId = 1 }],
        });
        _raidBosses.Setup(b => b.GetByIdAsync(14, default)).ReturnsAsync(new RaidBoss { Id = 14, Name = "Hydross the Unstable", RaidZoneId = 4 });

        var result = await _sut.HandleAsync(query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.BossNotTargetedByEvent);
    }

    [Fact]
    public async Task HandleAsync_BossIdGiven_ZoneTargetedByEvent_ScopesDefinitionsAndDropsFillsFromOtherDefinitions()
    {
        var query = new GetRaidEventAttributionsQuery { GuildId = GuildId, RequesterDiscordId = RequesterId, GuildBranchId = GuildBranchId, EventId = EventId, BossId = 14 };
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _raidEvents.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent
        {
            Id = EventId,
            PublicationStatus = RaidPublicationStatus.Published,
            TargetZones = [new RaidEventZone { RaidZoneId = 4 }],
            Assignments = [new RaidSlotAssignment { CharacterId = 100, SpecId = 265, Character = new Character { Id = 100, Name = "Aphrodisia", ClassId = 9 } }],
        });
        _raidBosses.Setup(b => b.GetByIdAsync(14, default)).ReturnsAsync(new RaidBoss { Id = 14, Name = "Hydross the Unstable", RaidZoneId = 4 });
        _definitions.Setup(d => d.GetForBranchAsync(GuildId, GuildBranchId, 14, default)).ReturnsAsync(
        [
            new GuildAttributionDefinition { Id = 2, GuildId = GuildId, RaidBossId = 14, Label = "Interrupt", SortOrder = 0, Cells = [] },
        ]);
        _attributions.Setup(a => a.GetForEventAsync(EventId, default)).ReturnsAsync(
        [
            // Belongs to the boss-scoped definition returned above — should be kept.
            new RaidEventAttribution { RaidEventId = EventId, GuildAttributionDefinitionId = 2, AttributionDefinitionCellId = 20, InstanceIndex = 0, CharacterId = 100 },
            // Belongs to some other (e.g. General) definition not part of this scope — should be dropped.
            new RaidEventAttribution { RaidEventId = EventId, GuildAttributionDefinitionId = 1, AttributionDefinitionCellId = 10, InstanceIndex = 0, CharacterId = 100 },
        ]);

        var result = await _sut.HandleAsync(query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Definitions.Should().ContainSingle(d => d.Id == 2 && d.RaidBossId == 14);
        var fill = result.Value!.Fills.Should().ContainSingle().Subject;
        fill.DefinitionId.Should().Be(2);
        fill.CellId.Should().Be(20);
    }

    // ── Guild-branch scoping ─────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_RosterMemberOfAnotherBranchOnly_ReturnsForbiddenUsingTheBranchAwareOverload()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, 99, default)).ReturnsAsync(GuildAccessLevel.Officer);

        var result = await _sut.HandleAsync(Query, default);

        result.Error.Should().Be(ResponseDetail.Forbidden);
        _access.Verify(a => a.GetAccessLevelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_GuildBranchNotFound_ReturnsGuildBranchNotFound()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _raidEvents.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent
        {
            Id = EventId, PublicationStatus = RaidPublicationStatus.Published, Assignments = [],
        });
        _branches.Setup(b => b.GetCurrentExpansionIdAsync(GuildId, GuildBranchId, default)).ReturnsAsync((int?)null);

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildBranchNotFound);
        _definitions.Verify(d => d.GetForBranchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int?>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SpellIconsAreResolvedOnTheEventsBranchExpansion()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _raidEvents.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent
        {
            Id = EventId, PublicationStatus = RaidPublicationStatus.Published, Assignments = [],
        });
        _definitions.Setup(d => d.GetForBranchAsync(GuildId, GuildBranchId, null, default)).ReturnsAsync(
        [
            new GuildAttributionDefinition
            {
                Id = 1, GuildId = GuildId, GuildBranchId = GuildBranchId, Label = "Bloodlust",
                SectionSpell = new Spell
                {
                    Id = 2825,
                    Availabilities =
                    [
                        new SpellAvailability { SpellId = 2825, ExpansionId = 12, IconUrl = "https://cdn/forever.jpg" },
                        new SpellAvailability { SpellId = 2825, ExpansionId = 2, IconUrl = "https://cdn/tbc.jpg" },
                    ],
                },
                Cells = [],
            },
        ]);

        var result = await _sut.HandleAsync(Query, default);

        result.Value!.Definitions.Single().SectionSpellIconUrl.Should().Be("https://cdn/tbc.jpg");
    }
}
