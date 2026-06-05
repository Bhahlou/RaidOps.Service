using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Guilds.Commands;
using RaidOps.Application.Implementations.Guilds.CommandHandlers;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Guilds.CommandHandlers;

public class UnregisterGuildCommandHandlerTests
{
    private readonly Mock<IGuildsRepository>         _guilds = new();
    private readonly UnregisterGuildCommandHandler   _sut;

    private const string GuildId = "guild-1";

    private static readonly UnregisterGuildCommand Command = new() { GuildId = GuildId };

    public UnregisterGuildCommandHandlerTests()
    {
        _sut = new UnregisterGuildCommandHandler(_guilds.Object);
    }

    [Fact]
    public async Task HandleAsync_AlwaysReturnsOk()
    {
        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_CallsUnregisterWithCorrectGuildId()
    {
        await _sut.HandleAsync(Command);

        _guilds.Verify(r => r.UnregisterAsync(GuildId, default), Times.Once);
    }
}
