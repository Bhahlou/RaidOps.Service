using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Characters.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Implementations.Dispatching;

namespace RaidOps.UnitTests.Application.Dispatching;

public class CommandDispatcherTests
{
    private readonly Mock<IServiceProvider>                               _services = new();
    private readonly Mock<ICommandHandlerAsync<DeactivateCharacterCommand>> _handler  = new();
    private readonly CommandDispatcher                                    _sut;

    private static readonly DeactivateCharacterCommand Command = new()
    {
        UserDiscordId = "user-1",
        CharacterId   = 42,
    };

    private static readonly Result<CommandResponse> OkResult =
        Result<CommandResponse>.Ok(new CommandResponse("ok"));

    public CommandDispatcherTests()
    {
        _services.Setup(sp => sp.GetService(typeof(ICommandHandlerAsync<DeactivateCharacterCommand>)))
            .Returns(_handler.Object);

        _sut = new CommandDispatcher(_services.Object);
    }

    [Fact]
    public async Task DispatchAsync_ResolvesHandlerAndDelegates()
    {
        _handler.Setup(h => h.HandleAsync(Command, default)).ReturnsAsync(OkResult);

        await _sut.DispatchAsync(Command);

        _handler.Verify(h => h.HandleAsync(Command, default), Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_ReturnsHandlerResult()
    {
        _handler.Setup(h => h.HandleAsync(Command, default)).ReturnsAsync(OkResult);

        var result = await _sut.DispatchAsync(Command);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Message.Should().Be("ok");
    }

    [Fact]
    public async Task DispatchAsync_HandlerNotRegistered_ThrowsInvalidOperationException()
    {
        _services.Setup(sp => sp.GetService(typeof(ICommandHandlerAsync<DeactivateCharacterCommand>)))
            .Returns((object?)null);

        var act = () => _sut.DispatchAsync(Command);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
