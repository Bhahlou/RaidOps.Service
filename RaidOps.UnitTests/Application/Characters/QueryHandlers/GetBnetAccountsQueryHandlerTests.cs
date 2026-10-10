using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Characters.Queries;
using RaidOps.Application.Implementations.Characters.QueryHandlers;
using RaidOps.Domain.Models.Character;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Characters.QueryHandlers;

public class GetBnetAccountsQueryHandlerTests
{
    private readonly Mock<IBnetAccountRepository> _bnetAccounts = new();
    private readonly GetBnetAccountsQueryHandler  _sut;

    private const string DiscordId = "user-1";

    private static readonly GetBnetAccountsQuery Query = new() { UserDiscordId = DiscordId };

    public GetBnetAccountsQueryHandlerTests()
    {
        _sut = new GetBnetAccountsQueryHandler(_bnetAccounts.Object);
    }

    [Fact]
    public async Task HandleAsync_NoLinkedAccounts_ReturnsOkWithEmptyList()
    {
        _bnetAccounts.Setup(r => r.GetAllByDiscordIdAsync(DiscordId, default))
            .ReturnsAsync((IReadOnlyList<BattleNetAccount>)[]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_OneLinkedAccount_ReturnsOkWithMappedFields()
    {
        var expiry = DateTimeOffset.UtcNow.AddHours(1);
        _bnetAccounts.Setup(r => r.GetAllByDiscordIdAsync(DiscordId, default))
            .ReturnsAsync((IReadOnlyList<BattleNetAccount>)
            [
                new BattleNetAccount
                {
                    UserDiscordId = DiscordId,
                    BnetId        = "bnet-42",
                    BattleTag     = "Player#1234",
                    AccessToken   = "tok",
                    Region        = "eu",
                    TokenExpiry   = expiry,
                },
            ]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value![0].BnetId.Should().Be("bnet-42");
        result.Value[0].BattleTag.Should().Be("Player#1234");
        result.Value[0].Region.Should().Be("eu");
        result.Value[0].TokenExpiry.Should().Be(expiry);
    }

    [Fact]
    public async Task HandleAsync_MultipleLinkedAccounts_ReturnsAllOfThem()
    {
        _bnetAccounts.Setup(r => r.GetAllByDiscordIdAsync(DiscordId, default))
            .ReturnsAsync((IReadOnlyList<BattleNetAccount>)
            [
                new BattleNetAccount { UserDiscordId = DiscordId, BnetId = "bnet-1", BattleTag = "Player#1234", AccessToken = "tok-1", Region = "eu" },
                new BattleNetAccount { UserDiscordId = DiscordId, BnetId = "bnet-2", BattleTag = "Player#5678", AccessToken = "tok-2", Region = "us" },
            ]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value!.Select(a => a.BnetId).Should().BeEquivalentTo(["bnet-1", "bnet-2"]);
    }
}
