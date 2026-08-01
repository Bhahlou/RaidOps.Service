using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Series.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Series.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Series.CommandHandlers;

public class DeactivateRaidSeriesCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IRaidSeriesRepository> _raidSeriesRepository = new();
    private readonly Mock<IRaidEventRepository> _raidEventRepository = new();
    private readonly Mock<IAuditLogService> _auditLogService = new();
    private readonly DeactivateRaidSeriesCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "officer-1";
    private const int SeriesId = 5;

    public DeactivateRaidSeriesCommandHandlerTests()
    {
        _sut = new DeactivateRaidSeriesCommandHandler(_access.Object, _raidSeriesRepository.Object, _raidEventRepository.Object, _auditLogService.Object);
    }

    private static DeactivateRaidSeriesCommand MakeCommand(bool deleteEmptyOccurrences = false) => new()
    {
        GuildId = GuildId,
        RequesterDiscordId = RequesterId,
        GuildBranchId = GuildBranchId,
        SeriesId = SeriesId,
        DeleteEmptyOccurrences = deleteEmptyOccurrences,
    };

    private void SetupOfficer() =>
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);

    [Fact]
    public async Task HandleAsync_NotOfficer_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_SeriesNotFound_ReturnsRaidSeriesNotFound()
    {
        SetupOfficer();
        _raidSeriesRepository.Setup(r => r.DeactivateAsync(SeriesId, GuildBranchId, default)).ReturnsAsync(false);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidSeriesNotFound);
        _raidEventRepository.Verify(r => r.DeleteEmptyDraftOccurrencesForSeriesAsync(It.IsAny<int>(), It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithoutDeleteEmptyOccurrences_DoesNotBulkDelete()
    {
        SetupOfficer();
        _raidSeriesRepository.Setup(r => r.DeactivateAsync(SeriesId, GuildBranchId, default)).ReturnsAsync(true);

        var result = await _sut.HandleAsync(MakeCommand(deleteEmptyOccurrences: false));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Body.Should().BeEquivalentTo(new { deletedCount = 0 });
        _raidEventRepository.Verify(r => r.DeleteEmptyDraftOccurrencesForSeriesAsync(It.IsAny<int>(), It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithDeleteEmptyOccurrences_BulkDeletesAndReportsCount()
    {
        SetupOfficer();
        _raidSeriesRepository.Setup(r => r.DeactivateAsync(SeriesId, GuildBranchId, default)).ReturnsAsync(true);
        _raidEventRepository.Setup(r => r.DeleteEmptyDraftOccurrencesForSeriesAsync(SeriesId, GuildBranchId, default)).ReturnsAsync(3);

        var result = await _sut.HandleAsync(MakeCommand(deleteEmptyOccurrences: true));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Body.Should().BeEquivalentTo(new { deletedCount = 3 });
        _auditLogService.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.RaidSeriesDeactivated,
            It.Is<Dictionary<string, string>>(d => d["seriesId"] == SeriesId.ToString() && d["deletedEmptyOccurrences"] == "3"),
            default), Times.Once);
    }
}
