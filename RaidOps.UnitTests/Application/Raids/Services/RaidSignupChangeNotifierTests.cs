using Moq;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Services;
using RaidOps.Domain.Models.Raids;

namespace RaidOps.UnitTests.Application.Raids.Services;

public class RaidSignupChangeNotifierTests
{
    private readonly Mock<IRaidSignupAnnouncementService> _raidSignupAnnouncementService = new();
    private readonly Mock<IRaidSignupNotifier> _raidSignupNotifier = new();
    private readonly RaidSignupChangeNotifier _sut;

    public RaidSignupChangeNotifierTests()
    {
        _sut = new RaidSignupChangeNotifier(_raidSignupAnnouncementService.Object, _raidSignupNotifier.Object);
    }

    private static RaidEvent MakeEvent() => new() { Id = 5, GuildBranchId = 10 };

    [Fact]
    public async Task NotifyChangedAsync_PublishesOrUpdatesTheSignupCallEmbed()
    {
        var raidEvent = MakeEvent();

        await _sut.NotifyChangedAsync(raidEvent);

        _raidSignupAnnouncementService.Verify(s => s.PublishOrUpdateSignupCallAsync(raidEvent, default), Times.Once);
    }

    [Fact]
    public async Task NotifyChangedAsync_NotifiesTheRaidSignupHubGroupWithBranchAndEventId()
    {
        var raidEvent = MakeEvent();

        await _sut.NotifyChangedAsync(raidEvent);

        _raidSignupNotifier.Verify(n => n.NotifyRaidSignupChangedAsync(10, 5, default), Times.Once);
    }

    [Fact]
    public async Task NotifyChangedAsync_ForwardsCancellationTokenToBothCollaborators()
    {
        using var cts = new CancellationTokenSource();
        var raidEvent = MakeEvent();

        await _sut.NotifyChangedAsync(raidEvent, cts.Token);

        _raidSignupAnnouncementService.Verify(s => s.PublishOrUpdateSignupCallAsync(raidEvent, cts.Token), Times.Once);
        _raidSignupNotifier.Verify(n => n.NotifyRaidSignupChangedAsync(10, 5, cts.Token), Times.Once);
    }
}
