using Microsoft.AspNetCore.SignalR;
using Moq;
using RaidOps.API.Hubs;
using Xunit;

namespace RaidOps.UnitTests.Hubs;

public class RaidSignupNotifierTests
{
    private readonly Mock<IClientProxy> _clientProxy = new();
    private readonly Mock<IHubClients> _clients = new();
    private readonly Mock<IHubContext<RaidSignupHub>> _hubContext = new();
    private readonly RaidSignupNotifier _sut;

    private const int GuildBranchId = 10;
    private const int EventId = 5;

    public RaidSignupNotifierTests()
    {
        _clients.Setup(c => c.Group(RaidSignupHub.GroupName(GuildBranchId, EventId))).Returns(_clientProxy.Object);
        _hubContext.Setup(h => h.Clients).Returns(_clients.Object);
        _sut = new RaidSignupNotifier(_hubContext.Object);
    }

    [Fact]
    public async Task NotifyRaidSignupChangedAsync_TargetsTheEventsGroup()
    {
        await _sut.NotifyRaidSignupChangedAsync(GuildBranchId, EventId);

        _clients.Verify(c => c.Group(RaidSignupHub.GroupName(GuildBranchId, EventId)), Times.Once);
    }

    [Fact]
    public async Task NotifyRaidSignupChangedAsync_SendsRaidSignupChangedEventWithTheEventId()
    {
        await _sut.NotifyRaidSignupChangedAsync(GuildBranchId, EventId);

        _clientProxy.Verify(p => p.SendCoreAsync("RaidSignupChanged", It.Is<object[]>(a => a.Length == 1 && (int)a[0]! == EventId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotifyRaidSignupChangedAsync_ForwardsCancellationToken()
    {
        using var cts = new CancellationTokenSource();

        await _sut.NotifyRaidSignupChangedAsync(GuildBranchId, EventId, cts.Token);

        _clientProxy.Verify(p => p.SendCoreAsync("RaidSignupChanged", It.IsAny<object[]>(), cts.Token), Times.Once);
    }
}
