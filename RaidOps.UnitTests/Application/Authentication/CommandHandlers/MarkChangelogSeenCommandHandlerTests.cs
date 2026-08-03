using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Authentication.Commands;
using RaidOps.Application.Implementations.Authentication.CommandHandlers;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Authentication.CommandHandlers;

public class MarkChangelogSeenCommandHandlerTests
{
    private readonly Mock<ISeenChangelogEntryRepository> _seenChangelog = new();
    private readonly MarkChangelogSeenCommandHandler      _sut;

    private const string RequesterId = "user-1";

    public MarkChangelogSeenCommandHandlerTests()
    {
        _sut = new MarkChangelogSeenCommandHandler(_seenChangelog.Object);
    }

    [Fact]
    public async Task HandleAsync_Success_RecordsSeenEntries()
    {
        var command = new MarkChangelogSeenCommand
        {
            RequesterDiscordId = RequesterId,
            EntryIds = ["2026-08-02-raid-notifications", "2026-08-01-raid-builder"],
        };

        var result = await _sut.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        _seenChangelog.Verify(r => r.MarkSeenAsync(
            RequesterId,
            It.Is<List<string>>(ids => ids.SequenceEqual(command.EntryIds)),
            default), Times.Once);
    }
}
