using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.CompositionPreviews.Queries;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.CompositionPreviews.QueryHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids.CompositionPreviews;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.CompositionPreviews.QueryHandlers;

public class GetRaidCompositionPreviewsQueryHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IRaidCompositionPreviewsRepository> _previews = new();
    private readonly GetRaidCompositionPreviewsQueryHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "officer-1";

    public GetRaidCompositionPreviewsQueryHandlerTests()
    {
        _sut = new GetRaidCompositionPreviewsQueryHandler(_access.Object, _previews.Object);
    }

    private static GetRaidCompositionPreviewsQuery MakeQuery() => new()
    {
        GuildId = GuildId,
        GuildBranchId = GuildBranchId,
        RequesterDiscordId = RequesterId,
    };

    [Fact]
    public async Task HandleAsync_NotOfficer_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_Success_MapsSummaries()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        var updatedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        _previews.Setup(r => r.GetForGuildBranchAsync(GuildBranchId, default)).ReturnsAsync(
        [
            new RaidCompositionPreview { Id = 1, Name = "40-man", GroupCount = 8, CreatedAt = DateTime.UtcNow, UpdatedAt = updatedAt },
        ]);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsSuccess.Should().BeTrue();
        var summary = result.Value!.Single();
        summary.Id.Should().Be(1);
        summary.Name.Should().Be("40-man");
        summary.GroupCount.Should().Be(8);
        summary.UpdatedAt.Should().Be(updatedAt);
    }

    [Fact]
    public async Task HandleAsync_Success_NeverUpdated_FallsBackToCreatedAt()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        var createdAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        _previews.Setup(r => r.GetForGuildBranchAsync(GuildBranchId, default)).ReturnsAsync(
        [
            new RaidCompositionPreview { Id = 1, Name = "40-man", GroupCount = 8, CreatedAt = createdAt, UpdatedAt = null },
        ]);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.Value!.Single().UpdatedAt.Should().Be(createdAt);
    }
}
