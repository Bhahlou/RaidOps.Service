using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Calendar.Availability.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Calendar.Availability.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Calendar;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Calendar.Availability.CommandHandlers;

public class UpdateRecurringAvailabilityPatternCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IAvailabilityRepository> _repository = new();
    private readonly Mock<IAuditLogService> _auditLog = new();
    private readonly UpdateRecurringAvailabilityPatternCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const string RequesterId = "user-1";
    private const int PatternId = 5;

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly DateOnly Anchor = new(2026, 1, 5);

    public UpdateRecurringAvailabilityPatternCommandHandlerTests()
    {
        _sut = new UpdateRecurringAvailabilityPatternCommandHandler(_access.Object, _repository.Object, _auditLog.Object);
    }

    private static UpdateRecurringAvailabilityPatternCommand MakeCommand(int cycleLengthDays, List<RecurringAvailabilityPatternDayInput> days) => new()
    {
        GuildId = GuildId,
        RequesterDiscordId = RequesterId,
        PatternId = PatternId,
        Label = "Updated pattern",
        CycleLengthDays = cycleLengthDays,
        AnchorDate = Anchor,
        Days = days,
    };

    private static RecurringAvailabilityPattern MakeExistingPattern(DateOnly effectiveFrom) => new()
    {
        Id = PatternId, UserDiscordId = RequesterId, GuildId = GuildId,
        CycleLengthDays = 7, AnchorDate = Anchor, EffectiveFrom = effectiveFrom, EffectiveUntil = null,
    };

    [Fact]
    public async Task HandleAsync_AccessBelowRoster_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Public);
        var command = MakeCommand(7, []);

        var result = await _sut.HandleAsync(command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task HandleAsync_CycleLengthDaysNotPositive_ReturnsInvalidRequest(int cycleLengthDays)
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        var command = MakeCommand(cycleLengthDays, []);

        var result = await _sut.HandleAsync(command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidRequest);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    public async Task HandleAsync_DayOffsetOutsideCycleLength_ReturnsInvalidRequest(int offsetInCycle)
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        var command = MakeCommand(7, [new RecurringAvailabilityPatternDayInput { OffsetInCycle = offsetInCycle, Status = DayAvailabilityStatus.Absent }]);

        var result = await _sut.HandleAsync(command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidRequest);
    }

    [Fact]
    public async Task HandleAsync_PartialDayWithoutEitherBound_ReturnsInvalidRequest()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        var command = MakeCommand(7, [new RecurringAvailabilityPatternDayInput { OffsetInCycle = 0, Status = DayAvailabilityStatus.Partial }]);

        var result = await _sut.HandleAsync(command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidRequest);
        _repository.Verify(r => r.AddPatternAsync(It.IsAny<RecurringAvailabilityPattern>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PatternNotFound_ReturnsRecurringAvailabilityPatternNotFound()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _repository.Setup(r => r.GetPatternByIdAsync(PatternId, RequesterId, GuildId, default)).ReturnsAsync((RecurringAvailabilityPattern?)null);
        var command = MakeCommand(7, []);

        var result = await _sut.HandleAsync(command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RecurringAvailabilityPatternNotFound);
    }

    [Fact]
    public async Task HandleAsync_ExistingPatternEffectiveFromInPast_ClosesOldVersionAndNeverDeletesThenAddsNewOne()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _repository.Setup(r => r.GetPatternByIdAsync(PatternId, RequesterId, GuildId, default))
            .ReturnsAsync(MakeExistingPattern(Today.AddDays(-5)));
        _repository.Setup(r => r.AddPatternAsync(It.IsAny<RecurringAvailabilityPattern>(), default))
            .ReturnsAsync((RecurringAvailabilityPattern p, CancellationToken _) => { p.Id = 6; return p; });
        var command = MakeCommand(7, [new RecurringAvailabilityPatternDayInput { OffsetInCycle = 0, Status = DayAvailabilityStatus.Absent }]);

        var result = await _sut.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        _repository.Verify(r => r.ClosePatternAsync(PatternId, RequesterId, GuildId, Today.AddDays(-1), default), Times.Once);
        _repository.Verify(r => r.DeletePatternAsync(PatternId, RequesterId, GuildId, default), Times.Never);
        _repository.Verify(r => r.AddPatternAsync(It.Is<RecurringAvailabilityPattern>(p => p.EffectiveFrom == Today && p.EffectiveUntil == null), default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ExistingPatternEffectiveFromToday_DeletesOldVersionAndNeverClosesThenAddsNewOne()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _repository.Setup(r => r.GetPatternByIdAsync(PatternId, RequesterId, GuildId, default))
            .ReturnsAsync(MakeExistingPattern(Today));
        _repository.Setup(r => r.DeletePatternAsync(PatternId, RequesterId, GuildId, default)).ReturnsAsync(true);
        _repository.Setup(r => r.AddPatternAsync(It.IsAny<RecurringAvailabilityPattern>(), default))
            .ReturnsAsync((RecurringAvailabilityPattern p, CancellationToken _) => { p.Id = 6; return p; });
        var command = MakeCommand(7, [new RecurringAvailabilityPatternDayInput { OffsetInCycle = 0, Status = DayAvailabilityStatus.Absent }]);

        var result = await _sut.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        _repository.Verify(r => r.DeletePatternAsync(PatternId, RequesterId, GuildId, default), Times.Once);
        _repository.Verify(r => r.ClosePatternAsync(PatternId, RequesterId, GuildId, It.IsAny<DateOnly>(), default), Times.Never);
        _repository.Verify(r => r.AddPatternAsync(It.Is<RecurringAvailabilityPattern>(p => p.EffectiveFrom == Today && p.EffectiveUntil == null), default), Times.Once);
    }
}
