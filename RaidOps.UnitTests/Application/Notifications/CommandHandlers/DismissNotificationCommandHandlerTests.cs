using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Notifications.Commands;
using RaidOps.Application.Implementations.Notifications.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Notifications.CommandHandlers;

public class DismissNotificationCommandHandlerTests
{
    private readonly Mock<INotificationDismissalRepository>  _dismissals = new();
    private readonly DismissNotificationCommandHandler       _sut;

    private const string RequesterId = "user-1";
    private const string GuildId     = "guild-1";

    public DismissNotificationCommandHandlerTests()
    {
        _sut = new DismissNotificationCommandHandler(_dismissals.Object);
    }

    [Fact]
    public async Task HandleAsync_Success_RecordsDismissal()
    {
        var command = new DismissNotificationCommand
        {
            RequesterDiscordId = RequesterId,
            Type               = NotificationType.OfficerThresholdNotConfigured,
            GuildId            = GuildId,
        };

        var result = await _sut.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        _dismissals.Verify(d => d.DismissAsync(RequesterId, NotificationType.OfficerThresholdNotConfigured, GuildId, default), Times.Once);
    }
}
