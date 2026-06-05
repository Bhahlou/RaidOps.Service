using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Characters.Queries;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Implementations.Characters.QueryHandlers;
using RaidOps.Domain.Models.Character;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Characters.QueryHandlers;

public class GetBnetAccountQueryHandlerTests
{
    private readonly Mock<IBnetAccountRepository>   _bnetAccounts = new();
    private readonly GetBnetAccountQueryHandler     _sut;

    private const string DiscordId = "user-1";

    private static readonly GetBnetAccountQuery Query = new() { UserDiscordId = DiscordId };

    public GetBnetAccountQueryHandlerTests()
    {
        _sut = new GetBnetAccountQueryHandler(_bnetAccounts.Object);
    }

    [Fact]
    public async Task HandleAsync_AccountFound_ReturnsOkWithMappedFields()
    {
        var expiry = DateTimeOffset.UtcNow.AddHours(1);
        _bnetAccounts.Setup(r => r.GetByDiscordIdAsync(DiscordId, default))
            .ReturnsAsync(new BattleNetAccount
            {
                UserDiscordId = DiscordId,
                BnetId        = "bnet-42",
                BattleTag     = "Player#1234",
                AccessToken   = "tok",
                Region        = "eu",
                TokenExpiry   = expiry,
            });

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.BnetId.Should().Be("bnet-42");
        result.Value.BattleTag.Should().Be("Player#1234");
        result.Value.Region.Should().Be("eu");
        result.Value.TokenExpiry.Should().Be(expiry);
    }

    [Fact]
    public async Task HandleAsync_AccountNotFound_ReturnsNotFound()
    {
        _bnetAccounts.Setup(r => r.GetByDiscordIdAsync(DiscordId, default))
            .ReturnsAsync((BattleNetAccount?)null);

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.NotFound);
    }
}
