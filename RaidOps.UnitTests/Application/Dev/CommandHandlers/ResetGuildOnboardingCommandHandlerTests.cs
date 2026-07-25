using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RaidOps.Application.Contracts.Dev.Commands;
using RaidOps.Application.Implementations.Dev.CommandHandlers;
using RaidOps.Domain.Models.Character;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Dev.CommandHandlers;

public class ResetGuildOnboardingCommandHandlerTests
{
    private readonly Mock<IBnetAccountRepository> _bnetAccounts = new();
    private readonly Mock<IGuildsRepository> _guilds = new();
    private readonly Mock<ILogger<ResetGuildOnboardingCommandHandler>> _logger = new();
    private readonly ResetGuildOnboardingCommandHandler _sut;

    private const string DiscordId = "user-1";
    private const string GuildId = "guild-1";

    private static readonly ResetGuildOnboardingCommand Command = new() { UserDiscordId = DiscordId, GuildId = GuildId };

    public ResetGuildOnboardingCommandHandlerTests()
    {
        _sut = new ResetGuildOnboardingCommandHandler(_bnetAccounts.Object, _guilds.Object, _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_NoLinkedAccounts_ResetsGuildOnboardingAndReturnsOk()
    {
        _bnetAccounts.Setup(r => r.GetAllByDiscordIdAsync(DiscordId, default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _bnetAccounts.Verify(r => r.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
        _guilds.Verify(g => g.ResetOnboardingAsync(GuildId, default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_MultipleLinkedAccounts_DeletesEachAndResetsGuildOnboarding()
    {
        _bnetAccounts.Setup(r => r.GetAllByDiscordIdAsync(DiscordId, default)).ReturnsAsync(
        [
            new BattleNetAccount { UserDiscordId = DiscordId, BnetId = "bnet-1" },
            new BattleNetAccount { UserDiscordId = DiscordId, BnetId = "bnet-2" },
        ]);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _bnetAccounts.Verify(r => r.DeleteAsync(DiscordId, "bnet-1", default), Times.Once);
        _bnetAccounts.Verify(r => r.DeleteAsync(DiscordId, "bnet-2", default), Times.Once);
        _guilds.Verify(g => g.ResetOnboardingAsync(GuildId, default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_InformationLoggingEnabled_DoesNotThrow()
    {
        _logger.Setup(l => l.IsEnabled(LogLevel.Information)).Returns(true);
        _bnetAccounts.Setup(r => r.GetAllByDiscordIdAsync(DiscordId, default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
    }
}
