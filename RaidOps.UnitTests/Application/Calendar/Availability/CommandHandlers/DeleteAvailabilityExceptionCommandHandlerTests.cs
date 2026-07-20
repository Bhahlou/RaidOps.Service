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

public class DeleteAvailabilityExceptionCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IAvailabilityRepository> _repository = new();
    private readonly Mock<IAuditLogService> _auditLog = new();
    private readonly DeleteAvailabilityExceptionCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const string RequesterId = "user-1";
    private const int ExceptionId = 7;

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static readonly DeleteAvailabilityExceptionCommand Command = new()
    {
        GuildId = GuildId,
        RequesterDiscordId = RequesterId,
        ExceptionId = ExceptionId,
    };

    public DeleteAvailabilityExceptionCommandHandlerTests()
    {
        _sut = new DeleteAvailabilityExceptionCommandHandler(_access.Object, _repository.Object, _auditLog.Object);
    }

    private static AvailabilityDeclaration MakeException(DateOnly endDate) => new()
    {
        Id = ExceptionId, UserDiscordId = RequesterId, GuildId = GuildId,
        StartDate = endDate, EndDate = endDate, Status = DayAvailabilityStatus.Absent,
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
    public async Task HandleAsync_ExceptionNotFound_ReturnsAvailabilityExceptionNotFound()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _repository.Setup(r => r.GetExceptionByIdAsync(ExceptionId, RequesterId, GuildId, default)).ReturnsAsync((AvailabilityDeclaration?)null);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.AvailabilityExceptionNotFound);
    }

    [Fact]
    public async Task HandleAsync_ExceptionAlreadyElapsed_ReturnsPastDeclarationLocked()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _repository.Setup(r => r.GetExceptionByIdAsync(ExceptionId, RequesterId, GuildId, default))
            .ReturnsAsync(MakeException(Today.AddDays(-1)));

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.PastDeclarationLocked);
    }

    [Fact]
    public async Task HandleAsync_ExceptionEndsToday_IsNotLockedAndSucceeds()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _repository.Setup(r => r.GetExceptionByIdAsync(ExceptionId, RequesterId, GuildId, default))
            .ReturnsAsync(MakeException(Today));
        _repository.Setup(r => r.DeleteExceptionAsync(ExceptionId, RequesterId, GuildId, default)).ReturnsAsync(true);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_DeleteRaceLosesAfterExceptionWasFound_ReturnsAvailabilityExceptionNotFound()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _repository.Setup(r => r.GetExceptionByIdAsync(ExceptionId, RequesterId, GuildId, default))
            .ReturnsAsync(MakeException(Today));
        _repository.Setup(r => r.DeleteExceptionAsync(ExceptionId, RequesterId, GuildId, default)).ReturnsAsync(false);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.AvailabilityExceptionNotFound);
    }

    [Fact]
    public async Task HandleAsync_Success_CallsDeleteExceptionWithExpectedIdsAndReturnsOk()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _repository.Setup(r => r.GetExceptionByIdAsync(ExceptionId, RequesterId, GuildId, default))
            .ReturnsAsync(MakeException(Today.AddDays(1)));
        _repository.Setup(r => r.DeleteExceptionAsync(ExceptionId, RequesterId, GuildId, default)).ReturnsAsync(true);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _repository.Verify(r => r.DeleteExceptionAsync(ExceptionId, RequesterId, GuildId, default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Success_LogsAuditVariablesWithEmptyTimesWhenNotPartial()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _repository.Setup(r => r.GetExceptionByIdAsync(ExceptionId, RequesterId, GuildId, default))
            .ReturnsAsync(MakeException(Today.AddDays(1)));
        _repository.Setup(r => r.DeleteExceptionAsync(ExceptionId, RequesterId, GuildId, default)).ReturnsAsync(true);

        await _sut.HandleAsync(Command);

        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.AvailabilityExceptionDeleted,
            It.Is<Dictionary<string, string>>(v => v["availableFrom"] == "" && v["availableUntil"] == ""),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Success_LogsAuditVariablesWithFormattedTimesWhenPartial()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        var existing = MakeException(Today.AddDays(1));
        existing.Status = DayAvailabilityStatus.Partial;
        existing.AvailableFrom = new TimeOnly(21, 30);
        existing.AvailableUntil = new TimeOnly(23, 0);
        _repository.Setup(r => r.GetExceptionByIdAsync(ExceptionId, RequesterId, GuildId, default)).ReturnsAsync(existing);
        _repository.Setup(r => r.DeleteExceptionAsync(ExceptionId, RequesterId, GuildId, default)).ReturnsAsync(true);

        await _sut.HandleAsync(Command);

        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.AvailabilityExceptionDeleted,
            It.Is<Dictionary<string, string>>(v => v["availableFrom"] == "21:30:00" && v["availableUntil"] == "23:00:00"),
            default), Times.Once);
    }
}
