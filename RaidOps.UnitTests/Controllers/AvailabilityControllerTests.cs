using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RaidOps.API.Controllers.v1;
using RaidOps.Application.Contracts.Calendar.Availability.Commands;
using RaidOps.Application.Contracts.Calendar.Availability.Queries;
using RaidOps.Application.Contracts.Calendar.Availability.Responses;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Domain.Enums;

namespace RaidOps.UnitTests.Controllers;

public class AvailabilityControllerTests
{
    private readonly Mock<ICommandDispatcher> _commands = new();
    private readonly Mock<IQueryDispatcher> _queries = new();
    private readonly AvailabilityController _sut;

    private const string DiscordId = "user-1";
    private const string GuildId = "guild-1";

    public AvailabilityControllerTests()
    {
        _sut = new AvailabilityController(_commands.Object, _queries.Object)
        {
            ControllerContext = ControllerTestHelpers.MakeContext(DiscordId)
        };
    }

    // ── GetMyAvailability ────────────────────────────────────────────────────

    [Fact]
    public async Task GetMyAvailability_SubMissing_ReturnsUnauthorized()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();

        var result = await _sut.GetMyAvailability(GuildId, default, default, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetMyAvailability_QueryFails_ReturnsBadRequest()
    {
        _queries.Setup(q => q.DispatchAsync<GetMyAvailabilityQuery, AvailabilityCalendarResponse>(
                It.IsAny<GetMyAvailabilityQuery>(), default))
            .ReturnsAsync(Result<AvailabilityCalendarResponse>.Fail(ResponseDetail.Forbidden));

        var result = await _sut.GetMyAvailability(GuildId, default, default, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetMyAvailability_Success_ReturnsOkWithResponse()
    {
        var response = new AvailabilityCalendarResponse();
        _queries.Setup(q => q.DispatchAsync<GetMyAvailabilityQuery, AvailabilityCalendarResponse>(
                It.IsAny<GetMyAvailabilityQuery>(), default))
            .ReturnsAsync(Result<AvailabilityCalendarResponse>.Ok(response));

        var result = await _sut.GetMyAvailability(GuildId, default, default, default);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(response);
    }

    [Fact]
    public async Task GetMyAvailability_PassesCorrectQueryFields()
    {
        var rangeStart = new DateOnly(2026, 1, 1);
        var rangeEnd = new DateOnly(2026, 1, 31);
        _queries.Setup(q => q.DispatchAsync<GetMyAvailabilityQuery, AvailabilityCalendarResponse>(
                It.IsAny<GetMyAvailabilityQuery>(), default))
            .ReturnsAsync(Result<AvailabilityCalendarResponse>.Ok(new AvailabilityCalendarResponse()));

        await _sut.GetMyAvailability(GuildId, rangeStart, rangeEnd, default);

        _queries.Verify(q => q.DispatchAsync<GetMyAvailabilityQuery, AvailabilityCalendarResponse>(
            It.Is<GetMyAvailabilityQuery>(x =>
                x.GuildId == GuildId && x.RequesterDiscordId == DiscordId &&
                x.RangeStart == rangeStart && x.RangeEnd == rangeEnd),
            default), Times.Once);
    }

    // ── CreateException ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateException_SubMissing_ReturnsUnauthorized()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();
        var command = new CreateAvailabilityExceptionCommand
        {
            StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 1, 1), Status = DayAvailabilityStatus.Absent,
        };

        var result = await _sut.CreateException(GuildId, command, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task CreateException_CommandFails_ReturnsBadRequest()
    {
        var command = new CreateAvailabilityExceptionCommand
        {
            StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 1, 1), Status = DayAvailabilityStatus.Absent,
        };
        _commands.Setup(c => c.DispatchAsync(It.IsAny<CreateAvailabilityExceptionCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.PastDeclarationLocked));

        var result = await _sut.CreateException(GuildId, command, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateException_Success_SetsRouteFieldsAndReturnsOk()
    {
        var command = new CreateAvailabilityExceptionCommand
        {
            StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 1, 1), Status = DayAvailabilityStatus.Absent,
        };
        _commands.Setup(c => c.DispatchAsync(It.IsAny<CreateAvailabilityExceptionCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));

        var result = await _sut.CreateException(GuildId, command, default);

        result.Should().BeOfType<OkObjectResult>();
        command.GuildId.Should().Be(GuildId);
        command.RequesterDiscordId.Should().Be(DiscordId);
    }

    // ── DeleteException ──────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteException_SubMissing_ReturnsUnauthorized()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();

        var result = await _sut.DeleteException(GuildId, 7, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task DeleteException_CommandFails_ReturnsBadRequest()
    {
        _commands.Setup(c => c.DispatchAsync(It.IsAny<DeleteAvailabilityExceptionCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.AvailabilityExceptionNotFound));

        var result = await _sut.DeleteException(GuildId, 7, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DeleteException_Success_PassesCorrectFieldsAndReturnsOk()
    {
        _commands.Setup(c => c.DispatchAsync(It.IsAny<DeleteAvailabilityExceptionCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));

        var result = await _sut.DeleteException(GuildId, 7, default);

        result.Should().BeOfType<OkObjectResult>();
        _commands.Verify(c => c.DispatchAsync(
            It.Is<DeleteAvailabilityExceptionCommand>(x => x.GuildId == GuildId && x.RequesterDiscordId == DiscordId && x.ExceptionId == 7),
            default), Times.Once);
    }

    // ── UpdateException ───────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateException_SubMissing_ReturnsUnauthorized()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();
        var command = new UpdateAvailabilityExceptionCommand
        {
            StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 1, 1), Status = DayAvailabilityStatus.Absent,
        };

        var result = await _sut.UpdateException(GuildId, 7, command, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task UpdateException_CommandFails_ReturnsBadRequest()
    {
        var command = new UpdateAvailabilityExceptionCommand
        {
            StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 1, 1), Status = DayAvailabilityStatus.Absent,
        };
        _commands.Setup(c => c.DispatchAsync(It.IsAny<UpdateAvailabilityExceptionCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest));

        var result = await _sut.UpdateException(GuildId, 7, command, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateException_Success_SetsRouteFieldsIncludingExceptionIdAndReturnsOk()
    {
        var command = new UpdateAvailabilityExceptionCommand
        {
            StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 1, 1), Status = DayAvailabilityStatus.Absent,
        };
        _commands.Setup(c => c.DispatchAsync(It.IsAny<UpdateAvailabilityExceptionCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));

        var result = await _sut.UpdateException(GuildId, 7, command, default);

        result.Should().BeOfType<OkObjectResult>();
        command.GuildId.Should().Be(GuildId);
        command.RequesterDiscordId.Should().Be(DiscordId);
        command.ExceptionId.Should().Be(7);
    }

    // ── RemoveExceptionDay ────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveExceptionDay_SubMissing_ReturnsUnauthorized()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();
        var command = new RemoveAvailabilityExceptionDayCommand { Date = new DateOnly(2026, 1, 2) };

        var result = await _sut.RemoveExceptionDay(GuildId, 7, command, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task RemoveExceptionDay_CommandFails_ReturnsBadRequest()
    {
        var command = new RemoveAvailabilityExceptionDayCommand { Date = new DateOnly(2026, 1, 2) };
        _commands.Setup(c => c.DispatchAsync(It.IsAny<RemoveAvailabilityExceptionDayCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest));

        var result = await _sut.RemoveExceptionDay(GuildId, 7, command, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RemoveExceptionDay_Success_SetsRouteFieldsIncludingExceptionIdAndReturnsOk()
    {
        var command = new RemoveAvailabilityExceptionDayCommand { Date = new DateOnly(2026, 1, 2) };
        _commands.Setup(c => c.DispatchAsync(It.IsAny<RemoveAvailabilityExceptionDayCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));

        var result = await _sut.RemoveExceptionDay(GuildId, 7, command, default);

        result.Should().BeOfType<OkObjectResult>();
        command.GuildId.Should().Be(GuildId);
        command.RequesterDiscordId.Should().Be(DiscordId);
        command.ExceptionId.Should().Be(7);
    }

    // ── CreatePattern ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreatePattern_SubMissing_ReturnsUnauthorized()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();
        var command = new CreateRecurringAvailabilityPatternCommand { CycleLengthDays = 7, AnchorDate = new DateOnly(2026, 1, 1), Days = [] };

        var result = await _sut.CreatePattern(GuildId, command, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task CreatePattern_CommandFails_ReturnsBadRequest()
    {
        var command = new CreateRecurringAvailabilityPatternCommand { CycleLengthDays = 7, AnchorDate = new DateOnly(2026, 1, 1), Days = [] };
        _commands.Setup(c => c.DispatchAsync(It.IsAny<CreateRecurringAvailabilityPatternCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest));

        var result = await _sut.CreatePattern(GuildId, command, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreatePattern_Success_SetsRouteFieldsAndReturnsOk()
    {
        var command = new CreateRecurringAvailabilityPatternCommand { CycleLengthDays = 7, AnchorDate = new DateOnly(2026, 1, 1), Days = [] };
        _commands.Setup(c => c.DispatchAsync(It.IsAny<CreateRecurringAvailabilityPatternCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));

        var result = await _sut.CreatePattern(GuildId, command, default);

        result.Should().BeOfType<OkObjectResult>();
        command.GuildId.Should().Be(GuildId);
        command.RequesterDiscordId.Should().Be(DiscordId);
    }

    // ── UpdatePattern ─────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdatePattern_SubMissing_ReturnsUnauthorized()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();
        var command = new UpdateRecurringAvailabilityPatternCommand { CycleLengthDays = 7, AnchorDate = new DateOnly(2026, 1, 1), Days = [] };

        var result = await _sut.UpdatePattern(GuildId, 5, command, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task UpdatePattern_CommandFails_ReturnsBadRequest()
    {
        var command = new UpdateRecurringAvailabilityPatternCommand { CycleLengthDays = 7, AnchorDate = new DateOnly(2026, 1, 1), Days = [] };
        _commands.Setup(c => c.DispatchAsync(It.IsAny<UpdateRecurringAvailabilityPatternCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.RecurringAvailabilityPatternNotFound));

        var result = await _sut.UpdatePattern(GuildId, 5, command, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdatePattern_Success_SetsRouteFieldsIncludingPatternIdAndReturnsOk()
    {
        var command = new UpdateRecurringAvailabilityPatternCommand { CycleLengthDays = 7, AnchorDate = new DateOnly(2026, 1, 1), Days = [] };
        _commands.Setup(c => c.DispatchAsync(It.IsAny<UpdateRecurringAvailabilityPatternCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));

        var result = await _sut.UpdatePattern(GuildId, 5, command, default);

        result.Should().BeOfType<OkObjectResult>();
        command.GuildId.Should().Be(GuildId);
        command.RequesterDiscordId.Should().Be(DiscordId);
        command.PatternId.Should().Be(5);
    }

    // ── DeletePattern ─────────────────────────────────────────────────────────

    [Fact]
    public async Task DeletePattern_SubMissing_ReturnsUnauthorized()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();

        var result = await _sut.DeletePattern(GuildId, 5, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task DeletePattern_CommandFails_ReturnsBadRequest()
    {
        _commands.Setup(c => c.DispatchAsync(It.IsAny<DeleteRecurringAvailabilityPatternCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.RecurringAvailabilityPatternNotFound));

        var result = await _sut.DeletePattern(GuildId, 5, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DeletePattern_Success_PassesCorrectFieldsAndReturnsOk()
    {
        _commands.Setup(c => c.DispatchAsync(It.IsAny<DeleteRecurringAvailabilityPatternCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));

        var result = await _sut.DeletePattern(GuildId, 5, default);

        result.Should().BeOfType<OkObjectResult>();
        _commands.Verify(c => c.DispatchAsync(
            It.Is<DeleteRecurringAvailabilityPatternCommand>(x => x.GuildId == GuildId && x.RequesterDiscordId == DiscordId && x.PatternId == 5),
            default), Times.Once);
    }
}
