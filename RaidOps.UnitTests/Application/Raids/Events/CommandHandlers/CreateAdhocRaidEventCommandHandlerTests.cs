using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Events.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Events.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Events.CommandHandlers;

public class CreateAdhocRaidEventCommandHandlerTests
{
    private readonly Mock<IRaidGridAndZoneValidator> _gridAndZoneValidator = new();
    private readonly Mock<IRaidEventRepository> _raidEventRepository = new();
    private readonly Mock<IRaidZoneRepository> _raidZoneRepository = new();
    private readonly Mock<IGuildsRepository> _guildsRepository = new();
    private readonly Mock<IGuildBranchesRepository> _guildBranchesRepository = new();
    private readonly Mock<IAuditLogService> _auditLogService = new();
    private readonly Mock<IRaidSignupAnnouncementService> _raidSignupAnnouncementService = new();
    private readonly CreateAdhocRaidEventCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "officer-1";

    public CreateAdhocRaidEventCommandHandlerTests()
    {
        _sut = new CreateAdhocRaidEventCommandHandler(
            _gridAndZoneValidator.Object, _raidEventRepository.Object, _raidZoneRepository.Object, _guildsRepository.Object,
            _guildBranchesRepository.Object, _auditLogService.Object, _raidSignupAnnouncementService.Object);

        _gridAndZoneValidator.Setup(v => v.ValidateAsync(RequesterId, GuildId, GuildBranchId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IEnumerable<int>>(), default))
            .ReturnsAsync(Result<List<int>>.Ok([1]));
    }

    private static CreateAdhocRaidEventCommand MakeCommand(
        int groupCount = 2, int slotsPerGroup = 5, List<int>? zoneIds = null,
        string? dedicatedAnnouncementChannelId = null, bool dedicatedAnnouncementChannelIsBotOwned = false,
        int? extendsRaidEventId = null) => new()
    {
        GuildId = GuildId,
        RequesterDiscordId = RequesterId,
        GuildBranchId = GuildBranchId,
        Name = "One-shot Kara clear",
        StartsAtUtc = new DateTime(2026, 2, 1, 20, 0, 0, DateTimeKind.Utc),
        GroupCount = groupCount,
        SlotsPerGroup = slotsPerGroup,
        RaidZoneIds = zoneIds ?? [1],
        DedicatedAnnouncementChannelId = dedicatedAnnouncementChannelId,
        DedicatedAnnouncementChannelIsBotOwned = dedicatedAnnouncementChannelIsBotOwned,
        ExtendsRaidEventId = extendsRaidEventId,
    };

    [Fact]
    public async Task HandleAsync_ValidatorFails_PropagatesErrorWithoutPersisting()
    {
        _gridAndZoneValidator.Setup(v => v.ValidateAsync(RequesterId, GuildId, GuildBranchId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IEnumerable<int>>(), default))
            .ReturnsAsync(Result<List<int>>.Fail(ResponseDetail.RaidZoneNotFound, "One or more raid zones do not exist."));

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidZoneNotFound);
        _raidEventRepository.Verify(r => r.AddAsync(It.IsAny<RaidEvent>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Success_CreatesDraftScheduledEventAndLogsAudit()
    {
        _raidEventRepository.Setup(r => r.AddAsync(It.IsAny<RaidEvent>(), default))
            .ReturnsAsync((RaidEvent e, CancellationToken _) => { e.Id = 77; return e; });
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Body.Should().BeEquivalentTo(new { Id = 77 });
        _raidEventRepository.Verify(r => r.AddAsync(It.Is<RaidEvent>(e =>
            e.GuildId == GuildId &&
            e.GuildBranchId == GuildBranchId &&
            e.RaidSeriesId == null &&
            e.Status == RaidEventStatus.Scheduled &&
            e.PublicationStatus == RaidPublicationStatus.Draft &&
            e.SignupMode == SignupMode.DefaultPresent &&
            e.CreatedByDiscordId == RequesterId &&
            e.TargetZones.Count == 1),
            default), Times.Once);
        _auditLogService.Verify(a => a.LogAsync(GuildId, RequesterId, GuildAuditAction.RaidEventCreated, It.IsAny<Dictionary<string, string>>(), default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Success_LogsAuditWithGuildLocalTimeAndZoneNames()
    {
        _raidEventRepository.Setup(r => r.AddAsync(It.IsAny<RaidEvent>(), default))
            .ReturnsAsync((RaidEvent e, CancellationToken _) => { e.Id = 77; return e; });
        _guildsRepository.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync(new Guild { Id = GuildId, Name = "G", Timezone = "Europe/Paris" });
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync(
        [
            new RaidZone { Id = 1, Name = "Molten Core" },
        ]);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        // 2026-02-01 20:00 UTC -> Europe/Paris is UTC+1 in February (no DST) -> 21:00 local.
        _auditLogService.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.RaidEventCreated,
            It.Is<Dictionary<string, string>>(d =>
                d["eventName"] == "One-shot Kara clear" &&
                d["startsAtLocal"] == "2026-02-01 21:00" &&
                d["raidZoneNames"] == "Molten Core"),
            default), Times.Once);
    }

    // ── DedicatedAnnouncementChannelIsBotOwned ──────────────────────────────

    [Fact]
    public async Task HandleAsync_SignupModeEvent_PublishesTheSignupCall()
    {
        _raidEventRepository.Setup(r => r.AddAsync(It.IsAny<RaidEvent>(), default))
            .ReturnsAsync((RaidEvent e, CancellationToken _) => { e.Id = 77; return e; });
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([]);

        var command = MakeCommand();
        command.SignupModeOverride = SignupMode.Signup;

        var result = await _sut.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        _raidSignupAnnouncementService.Verify(s => s.PublishOrUpdateSignupCallAsync(It.Is<RaidEvent>(e => e.Id == 77), default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_DefaultPresentEvent_NeverPublishesASignupCall()
    {
        _raidEventRepository.Setup(r => r.AddAsync(It.IsAny<RaidEvent>(), default))
            .ReturnsAsync((RaidEvent e, CancellationToken _) => { e.Id = 77; return e; });
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _raidSignupAnnouncementService.Verify(s => s.PublishOrUpdateSignupCallAsync(It.IsAny<RaidEvent>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ChannelIdSetAndBotOwnedTrue_PersistsBotOwnedTrue()
    {
        _raidEventRepository.Setup(r => r.AddAsync(It.IsAny<RaidEvent>(), default))
            .ReturnsAsync((RaidEvent e, CancellationToken _) => { e.Id = 77; return e; });
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(MakeCommand(dedicatedAnnouncementChannelId: "999", dedicatedAnnouncementChannelIsBotOwned: true));

        result.IsSuccess.Should().BeTrue();
        _raidEventRepository.Verify(r => r.AddAsync(It.Is<RaidEvent>(e =>
            e.DedicatedAnnouncementChannelId == "999" && e.DedicatedAnnouncementChannelIsBotOwned),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_BotOwnedTrueButNoChannelId_GuardsToFalse()
    {
        _raidEventRepository.Setup(r => r.AddAsync(It.IsAny<RaidEvent>(), default))
            .ReturnsAsync((RaidEvent e, CancellationToken _) => { e.Id = 77; return e; });
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([]);

        // A caller sending IsBotOwned=true without a channel id is nonsensical — the handler must
        // never persist a bot-owned flag with no channel behind it, or a later delete would try to
        // delete a null channel id.
        var result = await _sut.HandleAsync(MakeCommand(dedicatedAnnouncementChannelId: null, dedicatedAnnouncementChannelIsBotOwned: true));

        result.IsSuccess.Should().BeTrue();
        _raidEventRepository.Verify(r => r.AddAsync(It.Is<RaidEvent>(e =>
            e.DedicatedAnnouncementChannelId == null && !e.DedicatedAnnouncementChannelIsBotOwned),
            default), Times.Once);
    }

    // ── ExtendsRaidEventId ───────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_NoExtendsRaidEventId_PersistsNull()
    {
        _raidEventRepository.Setup(r => r.AddAsync(It.IsAny<RaidEvent>(), default))
            .ReturnsAsync((RaidEvent e, CancellationToken _) => { e.Id = 77; return e; });
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _raidEventRepository.Verify(r => r.AddAsync(It.Is<RaidEvent>(e => e.ExtendsRaidEventId == null), default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ExtendsRaidEventIdNotFound_ReturnsRaidEventNotFoundWithoutPersisting()
    {
        _raidEventRepository.Setup(r => r.GetByIdAsync(50, GuildBranchId, default)).ReturnsAsync((RaidEvent?)null);

        var result = await _sut.HandleAsync(MakeCommand(extendsRaidEventId: 50));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidEventNotFound);
        _raidEventRepository.Verify(r => r.AddAsync(It.IsAny<RaidEvent>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ExtendsStandaloneEvent_PersistsThatEventsIdDirectly()
    {
        _raidEventRepository.Setup(r => r.GetByIdAsync(50, GuildBranchId, default)).ReturnsAsync(new RaidEvent { Id = 50, ExtendsRaidEventId = null });
        _raidEventRepository.Setup(r => r.AddAsync(It.IsAny<RaidEvent>(), default))
            .ReturnsAsync((RaidEvent e, CancellationToken _) => { e.Id = 77; return e; });
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(MakeCommand(extendsRaidEventId: 50));

        result.IsSuccess.Should().BeTrue();
        _raidEventRepository.Verify(r => r.AddAsync(It.Is<RaidEvent>(e => e.ExtendsRaidEventId == 50), default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ExtendsAlreadyExtendingEvent_NormalizesToChainRoot()
    {
        // Event 50 already extends root event 10 — the new event must be flattened to point
        // directly at 10, never at the intermediate link (50).
        _raidEventRepository.Setup(r => r.GetByIdAsync(50, GuildBranchId, default)).ReturnsAsync(new RaidEvent { Id = 50, ExtendsRaidEventId = 10 });
        _raidEventRepository.Setup(r => r.AddAsync(It.IsAny<RaidEvent>(), default))
            .ReturnsAsync((RaidEvent e, CancellationToken _) => { e.Id = 77; return e; });
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(MakeCommand(extendsRaidEventId: 50));

        result.IsSuccess.Should().BeTrue();
        _raidEventRepository.Verify(r => r.AddAsync(It.Is<RaidEvent>(e => e.ExtendsRaidEventId == 10), default), Times.Once);
    }
}
