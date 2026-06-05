using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Characters.Commands;
using RaidOps.Application.Implementations.Characters.CommandHandlers;
using RaidOps.Domain.Models.Character;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Characters.CommandHandlers;

public class LinkBnetAccountCommandHandlerTests
{
    private readonly Mock<IBnetAccountRepository>    _bnetAccounts = new();
    private readonly LinkBnetAccountCommandHandler   _sut;

    private static readonly DateTimeOffset Expiry = DateTimeOffset.UtcNow.AddHours(1);

    private static readonly LinkBnetAccountCommand Command = new()
    {
        UserDiscordId = "user-1",
        BnetId        = "bnet-42",
        BattleTag     = "Player#1234",
        AccessToken   = "access-tok",
        RefreshToken  = "refresh-tok",
        TokenExpiry   = Expiry,
        Region        = "eu",
    };

    public LinkBnetAccountCommandHandlerTests()
    {
        _sut = new LinkBnetAccountCommandHandler(_bnetAccounts.Object);
    }

    [Fact]
    public async Task HandleAsync_UpsertsAccountAndReturnsOk()
    {
        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _bnetAccounts.Verify(r => r.UpsertAsync(It.IsAny<BattleNetAccount>(), default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_MapsAllFieldsFromCommandToAccount()
    {
        await _sut.HandleAsync(Command);

        _bnetAccounts.Verify(r => r.UpsertAsync(
            It.Is<BattleNetAccount>(a =>
                a.UserDiscordId == "user-1"  &&
                a.BnetId        == "bnet-42" &&
                a.BattleTag     == "Player#1234" &&
                a.AccessToken   == "access-tok" &&
                a.RefreshToken  == "refresh-tok" &&
                a.TokenExpiry   == Expiry &&
                a.Region        == "eu"),
            default), Times.Once);
    }
}
