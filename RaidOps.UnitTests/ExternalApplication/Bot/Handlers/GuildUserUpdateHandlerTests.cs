using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using RaidOps.Application.Contracts.Services;
using RaidOps.ExternalApplication.Implementations.Bot.Handlers;
using RaidOps.UnitTests.ExternalApplication.Bot;

namespace RaidOps.UnitTests.ExternalApplication.Bot.Handlers;

public class GuildUserUpdateHandlerTests
{
    private readonly Mock<IServiceScopeFactory>              _scopeFactory = new();
    private readonly Mock<IServiceScope>                     _scope        = new();
    private readonly Mock<IServiceProvider>                  _services     = new();
    private readonly Mock<IAuthNotifier>                     _authNotifier = new();
    private readonly Mock<ILogger<GuildUserUpdateHandler>>   _logger       = new();
    private readonly GuildUserUpdateHandler                  _sut;

    private const ulong UserId  = 511624657162731533UL;
    private const ulong GuildId = 796438478925725796UL;

    public GuildUserUpdateHandlerTests()
    {
        _scopeFactory.Setup(f => f.CreateScope()).Returns(_scope.Object);
        _scope.Setup(s => s.ServiceProvider).Returns(_services.Object);
        _services.Setup(sp => sp.GetService(typeof(IAuthNotifier))).Returns(_authNotifier.Object);

        _sut = new GuildUserUpdateHandler(_scopeFactory.Object, _logger.Object);
    }

    // loggerEnabled paramètre les deux branches du guard IsEnabled (CA1873).
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandleAsync_NotifiesTheUpdatedUser(bool loggerEnabled)
    {
        _logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(loggerEnabled);
        var arg = NetCordTestHelpers.MakeGuildUser(UserId, GuildId, roleIds: []);

        await _sut.HandleAsync(arg);

        _authNotifier.Verify(n => n.NotifyDiscordDataChangedAsync(UserId.ToString(), default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_NotifierThrows_DoesNotPropagateException()
    {
        var arg = NetCordTestHelpers.MakeGuildUser(UserId, GuildId, roleIds: []);
        _authNotifier.Setup(n => n.NotifyDiscordDataChangedAsync(It.IsAny<string>(), default))
            .ThrowsAsync(new Exception("unexpected"));

        var act = () => _sut.HandleAsync(arg).AsTask();

        await act.Should().NotThrowAsync();
    }
}
