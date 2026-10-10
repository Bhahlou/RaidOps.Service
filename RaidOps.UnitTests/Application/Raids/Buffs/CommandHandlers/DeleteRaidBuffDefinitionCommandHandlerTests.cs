using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Buffs.Commands;
using RaidOps.Application.Implementations.Raids.Buffs.CommandHandlers;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Buffs.CommandHandlers;

/// <summary>Unit tests for <see cref="DeleteRaidBuffDefinitionCommandHandler"/>.</summary>
public class DeleteRaidBuffDefinitionCommandHandlerTests
{
    private readonly Mock<IRaidBuffDefinitionsRepository> _definitions = new();
    private readonly DeleteRaidBuffDefinitionCommandHandler _sut;

    public DeleteRaidBuffDefinitionCommandHandlerTests()
    {
        _sut = new DeleteRaidBuffDefinitionCommandHandler(_definitions.Object);
    }

    [Fact]
    public async Task HandleAsync_RepositoryDeletes_ReturnsSuccess()
    {
        _definitions.Setup(d => d.DeleteAsync(7, default)).ReturnsAsync(true);

        var result = await _sut.HandleAsync(new DeleteRaidBuffDefinitionCommand { Id = 7 }, default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_RepositoryFindsNothing_ReturnsNotFound()
    {
        _definitions.Setup(d => d.DeleteAsync(7, default)).ReturnsAsync(false);

        var result = await _sut.HandleAsync(new DeleteRaidBuffDefinitionCommand { Id = 7 }, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.NotFound);
    }
}
