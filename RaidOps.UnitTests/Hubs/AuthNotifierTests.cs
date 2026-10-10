using Microsoft.AspNetCore.SignalR;
using Moq;
using RaidOps.API.Hubs;
using Xunit;

namespace RaidOps.UnitTests.Hubs;

public class AuthNotifierTests
{
    private readonly Mock<IClientProxy> _clientProxy = new();
    private readonly Mock<IHubClients>  _clients     = new();
    private readonly Mock<IHubContext<AuthHub>> _hubContext = new();
    private readonly AuthNotifier _sut;

    public AuthNotifierTests()
    {
        _clients.Setup(c => c.User(It.IsAny<string>())).Returns(_clientProxy.Object);
        _hubContext.Setup(h => h.Clients).Returns(_clients.Object);
        _sut = new AuthNotifier(_hubContext.Object);
    }

    [Fact]
    public async Task NotifyDiscordDataChangedAsync_TargetsTheGivenUser()
    {
        await _sut.NotifyDiscordDataChangedAsync("511624657162731533");

        _clients.Verify(c => c.User("511624657162731533"), Times.Once);
    }

    [Fact]
    public async Task NotifyDiscordDataChangedAsync_SendsDiscordDataChangedEvent()
    {
        await _sut.NotifyDiscordDataChangedAsync("123");

        _clientProxy.Verify(p => p.SendCoreAsync("DiscordDataChanged", It.IsAny<object[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotifyDiscordDataChangedAsync_ForwardsCancellationToken()
    {
        using var cts = new CancellationTokenSource();

        await _sut.NotifyDiscordDataChangedAsync("123", cts.Token);

        _clientProxy.Verify(p => p.SendCoreAsync("DiscordDataChanged", It.IsAny<object[]>(), cts.Token), Times.Once);
    }
}
