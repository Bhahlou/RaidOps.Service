using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Events.Queries;
using RaidOps.Application.Implementations.Raids.Events.QueryHandlers;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Raids;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Events.QueryHandlers;

public class GetUpcomingPublishedRaidEventChoicesQueryHandlerTests
{
    private readonly Mock<IRaidEventRepository> _raidEventRepository = new();
    private readonly Mock<IGuildsRepository> _guildsRepository = new();
    private readonly GetUpcomingPublishedRaidEventChoicesQueryHandler _sut;

    private const string GuildId = "guild-1";

    public GetUpcomingPublishedRaidEventChoicesQueryHandlerTests()
    {
        _sut = new GetUpcomingPublishedRaidEventChoicesQueryHandler(_raidEventRepository.Object, _guildsRepository.Object);
    }

    private static RaidEvent MakeEvent(int id, int guildBranchId, string name, DateTime startsAtUtc) => new()
    {
        Id = id,
        GuildBranchId = guildBranchId,
        Name = name,
        StartsAtUtc = startsAtUtc,
        GuildBranch = new GuildBranch { Id = guildBranchId, Branch = new Branch { Id = 1, Name = "Classic Era" } },
    };

    [Fact]
    public async Task HandleAsync_GuildTimezoneConfigured_ConvertsStartsAtToGuildLocal()
    {
        var startsAtUtc = new DateTime(2026, 2, 1, 20, 0, 0, DateTimeKind.Utc);
        _raidEventRepository.Setup(r => r.GetUpcomingPublishedForGuildAsync(GuildId, It.IsAny<DateTime>(), 25, default))
            .ReturnsAsync([MakeEvent(1, 10, "Split 1", startsAtUtc)]);
        _guildsRepository.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync(new Guild { Id = GuildId, Name = "G", Timezone = "Europe/Paris" });

        var result = await _sut.HandleAsync(new GetUpcomingPublishedRaidEventChoicesQuery { GuildId = GuildId }, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        var choice = result.Value![0];
        choice.Id.Should().Be(1);
        choice.GuildBranchId.Should().Be(10);
        choice.Name.Should().Be("Split 1");
        choice.BranchName.Should().Be("Classic Era");
        choice.StartsAtLocal.Should().Be(new DateTime(2026, 2, 1, 21, 0, 0)); // CET/CEST +1 in February
    }

    [Fact]
    public async Task HandleAsync_GuildNotFound_FallsBackToUnshiftedUtc()
    {
        var startsAtUtc = new DateTime(2026, 2, 1, 20, 0, 0, DateTimeKind.Utc);
        _raidEventRepository.Setup(r => r.GetUpcomingPublishedForGuildAsync(GuildId, It.IsAny<DateTime>(), 25, default))
            .ReturnsAsync([MakeEvent(1, 10, "Split 1", startsAtUtc)]);
        _guildsRepository.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync((Guild?)null);

        var result = await _sut.HandleAsync(new GetUpcomingPublishedRaidEventChoicesQuery { GuildId = GuildId }, default);

        result.IsSuccess.Should().BeTrue();
        result.Value![0].StartsAtLocal.Should().Be(startsAtUtc);
    }

    [Fact]
    public async Task HandleAsync_NoUpcomingEvents_ReturnsEmptyList()
    {
        _raidEventRepository.Setup(r => r.GetUpcomingPublishedForGuildAsync(GuildId, It.IsAny<DateTime>(), 25, default)).ReturnsAsync([]);
        _guildsRepository.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync(new Guild { Id = GuildId, Name = "G" });

        var result = await _sut.HandleAsync(new GetUpcomingPublishedRaidEventChoicesQuery { GuildId = GuildId }, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_MultipleEvents_PreservesRepositoryOrder()
    {
        _raidEventRepository.Setup(r => r.GetUpcomingPublishedForGuildAsync(GuildId, It.IsAny<DateTime>(), 25, default))
            .ReturnsAsync(
            [
                MakeEvent(1, 10, "Split 1", new DateTime(2026, 2, 1, 20, 0, 0, DateTimeKind.Utc)),
                MakeEvent(2, 11, "Split 2", new DateTime(2026, 2, 3, 20, 0, 0, DateTimeKind.Utc)),
            ]);
        _guildsRepository.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync(new Guild { Id = GuildId, Name = "G" });

        var result = await _sut.HandleAsync(new GetUpcomingPublishedRaidEventChoicesQuery { GuildId = GuildId }, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().SatisfyRespectively(
            c => c.Id.Should().Be(1),
            c => c.Id.Should().Be(2));
    }
}
