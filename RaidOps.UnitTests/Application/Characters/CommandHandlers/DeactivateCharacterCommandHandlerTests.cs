using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Characters.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Implementations.Characters.CommandHandlers;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Characters.CommandHandlers;

public class DeactivateCharacterCommandHandlerTests
{
    private readonly Mock<ICharacterRepository> _characters = new();
    private readonly DeactivateCharacterCommandHandler _sut;

    private const string DiscordId   = "user-1";
    private const int    CharacterId = 42;

    private static readonly DeactivateCharacterCommand Command = new()
    {
        UserDiscordId = DiscordId,
        CharacterId   = CharacterId,
    };

    public DeactivateCharacterCommandHandlerTests()
    {
        _sut = new DeactivateCharacterCommandHandler(_characters.Object);
    }

    [Fact]
    public async Task HandleAsync_CharacterDeactivated_ReturnsOk()
    {
        _characters.Setup(r => r.DeactivateAsync(CharacterId, DiscordId, default))
            .ReturnsAsync(true);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _characters.Verify(r => r.DeactivateAsync(CharacterId, DiscordId, default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_CharacterNotFound_ReturnsNotFound()
    {
        _characters.Setup(r => r.DeactivateAsync(CharacterId, DiscordId, default))
            .ReturnsAsync(false);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.NotFound);
    }
}
