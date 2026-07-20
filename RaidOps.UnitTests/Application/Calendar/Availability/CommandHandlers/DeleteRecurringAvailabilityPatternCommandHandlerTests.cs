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

public class DeleteRecurringAvailabilityPatternCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IAvailabilityRepository> _repository = new();
    private readonly Mock<IAuditLogService> _auditLog = new();
    private readonly DeleteRecurringAvailabilityPatternCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const string RequesterId = "user-1";
    private const int PatternId = 5;

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly DateOnly Anchor = new(2026, 1, 5);

    private static readonly DeleteRecurringAvailabilityPatternCommand Command = new()
    {
        GuildId = GuildId,
        RequesterDiscordId = RequesterId,
        PatternId = PatternId,
    };

    public DeleteRecurringAvailabilityPatternCommandHandlerTests()
    {
        _sut = new DeleteRecurringAvailabilityPatternCommandHandler(_access.Object, _repository.Object, _auditLog.Object);
    }

    private static RecurringAvailabilityPattern MakeExistingPattern(DateOnly effectiveFrom) => new()
    {
        Id = PatternId, UserDiscordId = RequesterId, GuildId = GuildId,
        CycleLengthDays = 7, AnchorDate = Anchor, EffectiveFrom = effectiveFrom, EffectiveUntil = null,
    };

    [Fact]
    public async Task HandleAsync_AccessBelowRoster_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Public);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_PatternNotFound_ReturnsRecurringAvailabilityPatternNotFound()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _repository.Setup(r => r.GetPatternByIdAsync(PatternId, RequesterId, GuildId, default)).ReturnsAsync((RecurringAvailabilityPattern?)null);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RecurringAvailabilityPatternNotFound);
    }

    [Fact]
    public async Task HandleAsync_EffectiveFromInPast_ClosesPatternAndNeverDeletesThenReturnsOk()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _repository.Setup(r => r.GetPatternByIdAsync(PatternId, RequesterId, GuildId, default))
            .ReturnsAsync(MakeExistingPattern(Today.AddDays(-5)));
        _repository.Setup(r => r.ClosePatternAsync(PatternId, RequesterId, GuildId, Today.AddDays(-1), default)).ReturnsAsync(true);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _repository.Verify(r => r.ClosePatternAsync(PatternId, RequesterId, GuildId, Today.AddDays(-1), default), Times.Once);
        _repository.Verify(r => r.DeletePatternAsync(PatternId, RequesterId, GuildId, default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_EffectiveFromToday_DeletesPatternAndNeverClosesThenReturnsOk()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _repository.Setup(r => r.GetPatternByIdAsync(PatternId, RequesterId, GuildId, default))
            .ReturnsAsync(MakeExistingPattern(Today));
        _repository.Setup(r => r.DeletePatternAsync(PatternId, RequesterId, GuildId, default)).ReturnsAsync(true);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _repository.Verify(r => r.DeletePatternAsync(PatternId, RequesterId, GuildId, default), Times.Once);
        _repository.Verify(r => r.ClosePatternAsync(PatternId, RequesterId, GuildId, It.IsAny<DateOnly>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_StopCallReturnsFalse_ReturnsRecurringAvailabilityPatternNotFound()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _repository.Setup(r => r.GetPatternByIdAsync(PatternId, RequesterId, GuildId, default))
            .ReturnsAsync(MakeExistingPattern(Today));
        _repository.Setup(r => r.DeletePatternAsync(PatternId, RequesterId, GuildId, default)).ReturnsAsync(false);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RecurringAvailabilityPatternNotFound);
    }
}
