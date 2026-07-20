using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Calendar.Availability.Queries;
using RaidOps.Application.Contracts.Calendar.Availability.Responses;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Calendar.Availability.QueryHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Calendar;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Calendar.Availability.QueryHandlers;

public class GetMyAvailabilityQueryHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IAvailabilityRepository> _repository = new();
    private readonly Mock<IAvailabilityResolutionService> _resolutionService = new();
    private readonly GetMyAvailabilityQueryHandler _sut;

    private const string GuildId = "guild-1";
    private const string RequesterId = "user-1";

    private static readonly GetMyAvailabilityQuery Query = new()
    {
        GuildId = GuildId,
        RequesterDiscordId = RequesterId,
        RangeStart = new DateOnly(2026, 1, 1),
        RangeEnd = new DateOnly(2026, 1, 7),
    };

    public GetMyAvailabilityQueryHandlerTests()
    {
        _sut = new GetMyAvailabilityQueryHandler(_access.Object, _repository.Object, _resolutionService.Object);
    }

    [Fact]
    public async Task HandleAsync_AccessBelowRoster_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Public);

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_RangeEndBeforeRangeStart_ReturnsInvalidRequest()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        var query = new GetMyAvailabilityQuery
        {
            GuildId = GuildId, RequesterDiscordId = RequesterId,
            RangeStart = new DateOnly(2026, 1, 7), RangeEnd = new DateOnly(2026, 1, 1),
        };

        var result = await _sut.HandleAsync(query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidRequest);
    }

    [Fact]
    public async Task HandleAsync_Success_ReturnsResolvedDaysAndMappedExceptionsAndOnlyOpenPatterns()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var exception = new AvailabilityDeclaration
        {
            Id = 1, UserDiscordId = RequesterId, GuildId = GuildId,
            StartDate = new DateOnly(2026, 1, 2), EndDate = new DateOnly(2026, 1, 3),
            Status = DayAvailabilityStatus.Absent, Reason = "Sick",
            AvailableFrom = new TimeOnly(8, 0), AvailableUntil = new TimeOnly(16, 0),
        };
        var exceptions = new List<AvailabilityDeclaration> { exception };
        _repository.Setup(r => r.GetExceptionsOverlappingAsync(RequesterId, GuildId, Query.RangeStart, Query.RangeEnd, default))
            .ReturnsAsync(exceptions);

        var closedPattern = new RecurringAvailabilityPattern
        {
            Id = 10, UserDiscordId = RequesterId, GuildId = GuildId,
            CycleLengthDays = 7, AnchorDate = new DateOnly(2026, 1, 1),
            EffectiveFrom = new DateOnly(2025, 1, 1), EffectiveUntil = new DateOnly(2025, 12, 31),
            Days = [],
        };
        var openPattern = new RecurringAvailabilityPattern
        {
            Id = 11, UserDiscordId = RequesterId, GuildId = GuildId,
            CycleLengthDays = 7, AnchorDate = new DateOnly(2026, 1, 1),
            EffectiveFrom = new DateOnly(2026, 1, 1), EffectiveUntil = null,
            Days = [new RecurringAvailabilityPatternDay
            {
                OffsetInCycle = 2, Status = DayAvailabilityStatus.Partial, Reason = "Night shift",
                AvailableFrom = new TimeOnly(18, 0), AvailableUntil = new TimeOnly(22, 0),
            }],
        };
        var patterns = new List<RecurringAvailabilityPattern> { closedPattern, openPattern };
        _repository.Setup(r => r.GetPatternsAsync(RequesterId, GuildId, default)).ReturnsAsync(patterns);

        var resolvedDays = new List<ResolvedDayAvailabilityResponse>
        {
            new() { Date = new DateOnly(2026, 1, 1), Status = DayAvailabilityStatus.Available },
            new() { Date = new DateOnly(2026, 1, 2), Status = DayAvailabilityStatus.Absent, Reason = "Sick", IsException = true },
        };
        _resolutionService
            .Setup(s => s.Resolve(Query.RangeStart, Query.RangeEnd, exceptions, patterns))
            .Returns(resolvedDays);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Days.Should().BeEquivalentTo(resolvedDays);

        result.Value.Exceptions.Should().ContainSingle();
        var mappedException = result.Value.Exceptions[0];
        mappedException.Id.Should().Be(exception.Id);
        mappedException.StartDate.Should().Be(exception.StartDate);
        mappedException.EndDate.Should().Be(exception.EndDate);
        mappedException.Status.Should().Be(exception.Status);
        mappedException.Reason.Should().Be(exception.Reason);
        mappedException.AvailableFrom.Should().Be(exception.AvailableFrom);
        mappedException.AvailableUntil.Should().Be(exception.AvailableUntil);

        result.Value.Patterns.Should().ContainSingle();
        var mappedPattern = result.Value.Patterns[0];
        mappedPattern.Id.Should().Be(openPattern.Id);
        mappedPattern.Days.Should().ContainSingle();
        var mappedDay = mappedPattern.Days[0];
        mappedDay.OffsetInCycle.Should().Be(2);
        mappedDay.Status.Should().Be(DayAvailabilityStatus.Partial);
        mappedDay.Reason.Should().Be("Night shift");
        mappedDay.AvailableFrom.Should().Be(new TimeOnly(18, 0));
        mappedDay.AvailableUntil.Should().Be(new TimeOnly(22, 0));

        _resolutionService.Verify(s => s.Resolve(Query.RangeStart, Query.RangeEnd, exceptions, patterns), Times.Once);
    }
}
