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

public class CreateAvailabilityExceptionCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IAvailabilityRepository> _repository = new();
    private readonly Mock<IAvailabilityChangeAnnouncer> _announcer = new();
    private readonly CreateAvailabilityExceptionCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "user-1";

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    public CreateAvailabilityExceptionCommandHandlerTests()
    {
        _repository.Setup(r => r.GetExceptionsOverlappingAsync(
                It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _repository.Setup(r => r.GetPatternsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _sut = new CreateAvailabilityExceptionCommandHandler(_access.Object, _repository.Object, _announcer.Object);
    }

    private static CreateAvailabilityExceptionCommand MakeCommand(DateOnly startDate, DateOnly endDate) => new()
    {
        GuildId = GuildId,
        GuildBranchId = GuildBranchId,
        RequesterDiscordId = RequesterId,
        StartDate = startDate,
        EndDate = endDate,
        Status = DayAvailabilityStatus.Absent,
        Reason = "Vacation",
        AvailableFrom = null,
        AvailableUntil = null,
    };

    [Fact]
    public async Task HandleAsync_GuildIdSetButGuildBranchIdNull_ReturnsInvalidRequest()
    {
        var command = MakeCommand(Today, Today);
        command.GuildBranchId = null;

        var result = await _sut.HandleAsync(command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidRequest);
        _access.Verify(a => a.GetAccessLevelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AccessBelowRoster_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Public);
        var command = MakeCommand(Today, Today);

        var result = await _sut.HandleAsync(command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_Global_SkipsAccessCheckAndSucceeds()
    {
        var command = MakeCommand(Today, Today);
        command.GuildId = null;
        command.GuildBranchId = null;
        _repository.Setup(r => r.AddExceptionAsync(It.IsAny<AvailabilityDeclaration>(), default))
            .ReturnsAsync((AvailabilityDeclaration e, CancellationToken _) => { e.Id = 42; return e; });

        var result = await _sut.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        _access.Verify(a => a.GetAccessLevelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.AddExceptionAsync(It.Is<AvailabilityDeclaration>(e => e.GuildId == null && e.GuildBranchId == null), default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_EndDateBeforeStartDate_ReturnsInvalidRequest()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        var command = MakeCommand(Today.AddDays(1), Today);

        var result = await _sut.HandleAsync(command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidRequest);
    }

    [Fact]
    public async Task HandleAsync_StartDateInThePast_ReturnsPastDeclarationLocked()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        var command = MakeCommand(Today.AddDays(-1), Today);

        var result = await _sut.HandleAsync(command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.PastDeclarationLocked);
    }

    [Fact]
    public async Task HandleAsync_PartialWithoutEitherBound_ReturnsInvalidRequest()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        var command = MakeCommand(Today, Today);
        command.Status = DayAvailabilityStatus.Partial;

        var result = await _sut.HandleAsync(command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidRequest);
        _repository.Verify(r => r.AddExceptionAsync(It.IsAny<AvailabilityDeclaration>(), default), Times.Never);
    }

    [Theory]
    [InlineData("09:00", null)]
    [InlineData(null, "13:00")]
    public async Task HandleAsync_PartialWithAtLeastOneBound_Succeeds(string? from, string? until)
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        var command = MakeCommand(Today, Today);
        command.Status = DayAvailabilityStatus.Partial;
        command.AvailableFrom = from != null ? TimeOnly.Parse(from) : null;
        command.AvailableUntil = until != null ? TimeOnly.Parse(until) : null;
        _repository.Setup(r => r.AddExceptionAsync(It.IsAny<AvailabilityDeclaration>(), default))
            .ReturnsAsync((AvailabilityDeclaration e, CancellationToken _) => { e.Id = 42; return e; });

        var result = await _sut.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_StartDateToday_IsNotLockedAndSucceeds()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        var command = MakeCommand(Today, Today);
        _repository.Setup(r => r.AddExceptionAsync(It.IsAny<AvailabilityDeclaration>(), default))
            .ReturnsAsync((AvailabilityDeclaration e, CancellationToken _) => { e.Id = 42; return e; });

        var result = await _sut.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_Success_CallsAddExceptionWithExpectedFieldsAndReturnsOkWithId()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        var command = new CreateAvailabilityExceptionCommand
        {
            GuildId = GuildId,
            GuildBranchId = GuildBranchId,
            RequesterDiscordId = RequesterId,
            StartDate = Today,
            EndDate = Today.AddDays(2),
            Status = DayAvailabilityStatus.Partial,
            Reason = "Half day",
            AvailableFrom = new TimeOnly(9, 0),
            AvailableUntil = new TimeOnly(13, 0),
        };
        _repository.Setup(r => r.AddExceptionAsync(It.IsAny<AvailabilityDeclaration>(), default))
            .ReturnsAsync((AvailabilityDeclaration e, CancellationToken _) => { e.Id = 42; return e; });

        var result = await _sut.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        _repository.Verify(r => r.AddExceptionAsync(It.Is<AvailabilityDeclaration>(e =>
            e.UserDiscordId == RequesterId && e.GuildId == GuildId && e.GuildBranchId == GuildBranchId &&
            e.StartDate == command.StartDate && e.EndDate == command.EndDate &&
            e.Status == DayAvailabilityStatus.Partial && e.Reason == "Half day" &&
            e.AvailableFrom == new TimeOnly(9, 0) && e.AvailableUntil == new TimeOnly(13, 0)),
            default), Times.Once);
        result.Value!.Body.Should().BeEquivalentTo(new { Id = 42 });
    }
}
