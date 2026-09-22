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

    private static readonly ReorderGuildAttributionDefinitionsCommand Command = new()
    {
        GuildId = GuildId, RequesterDiscordId = RequesterId, OrderedIds = [3, 1, 2],
    };

    public ReorderGuildAttributionDefinitionsCommandHandlerTests()
    {
        _sut = new ReorderGuildAttributionDefinitionsCommandHandler(_access.Object, _definitions.Object);
    }

    [Fact]
    public async Task HandleAsync_NotOfficer_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
        _definitions.Verify(d => d.ReorderAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Success_ReordersDefinitions()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _definitions.Verify(d => d.ReorderAsync(GuildId, Command.OrderedIds, default), Times.Once);
    }
}
