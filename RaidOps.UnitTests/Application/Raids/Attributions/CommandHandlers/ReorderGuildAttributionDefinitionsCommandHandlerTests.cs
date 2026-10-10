using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Attributions.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Attributions.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Attributions.CommandHandlers;

/// <summary>
/// Unit tests for <see cref="ReorderGuildAttributionDefinitionsCommandHandler"/>.
/// </summary>
public class ReorderGuildAttributionDefinitionsCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IGuildAttributionDefinitionsRepository> _definitions = new();
    private readonly ReorderGuildAttributionDefinitionsCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const string RequesterId = "officer-1";
    private const int BranchId = 5;

    private static readonly ReorderGuildAttributionDefinitionsCommand Command = new()
    {
        GuildId = GuildId, GuildBranchId = BranchId, RequesterDiscordId = RequesterId, OrderedIds = [3, 1, 2],
    };

    public ReorderGuildAttributionDefinitionsCommandHandlerTests()
    {
        _sut = new ReorderGuildAttributionDefinitionsCommandHandler(_access.Object, _definitions.Object);
    }

    [Fact]
    public async Task HandleAsync_NotOfficer_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, BranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
        _definitions.Verify(d => d.ReorderAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<int>>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Success_ReordersDefinitions()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, BranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _definitions.Verify(d => d.ReorderAsync(GuildId, BranchId, Command.OrderedIds, default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_OfficerOfAnotherBranchOnly_ReturnsForbiddenUsingTheBranchAwareOverload()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, 99, default)).ReturnsAsync(GuildAccessLevel.Officer);

        var result = await _sut.HandleAsync(Command);

        result.Error.Should().Be(ResponseDetail.Forbidden);
        _access.Verify(a => a.GetAccessLevelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _definitions.Verify(d => d.ReorderAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<int>>(), default), Times.Never);
    }
}
