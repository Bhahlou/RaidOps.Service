using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Guilds.Settings.Queries;
using RaidOps.Application.Implementations.Guilds.Settings.QueryHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Guilds.Settings.QueryHandlers;

public class GetGuildSettingsQueryHandlerTests
{
    private readonly Mock<IGuildsRepository>     _guilds = new();
    private readonly GetGuildSettingsQueryHandler _sut;

    private const string GuildId     = "guild-1";
    private const string RequesterId = "user-1";

    private static readonly GetGuildSettingsQuery Query = new()
    {
        GuildId            = GuildId,
        RequesterDiscordId = RequesterId,
    };

    public GetGuildSettingsQueryHandlerTests()
    {
        _sut = new GetGuildSettingsQueryHandler(_guilds.Object);
    }

    [Fact]
    public async Task HandleAsync_GuildNotFound_ReturnsGuildNotFound()
    {
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync((Guild?)null);

        var result = await _sut.HandleAsync(Query);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildNotFound);
    }

    [Fact]
    public async Task HandleAsync_GuildNotRegistered_ReturnsGuildNotFound()
    {
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = false });

        var result = await _sut.HandleAsync(Query);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildNotFound);
    }

    [Fact]
    public async Task HandleAsync_Success_ReturnsMappedSettings()
    {
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild
            {
                Id           = GuildId,
                Name         = "Test",
                IsRegistered = true,
                Timezone     = "Europe/Paris",
                RosterMode   = RosterMode.Open,
                MinRosterRoleId = "role-abc",
            });

        var result = await _sut.HandleAsync(Query);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Timezone.Should().Be("Europe/Paris");
        result.Value!.RosterMode.Should().Be(RosterMode.Open);
        result.Value!.MinRosterRoleId.Should().Be("role-abc");
    }

    [Fact]
    public async Task HandleAsync_NullRosterMode_DefaultsToOpen()
    {
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true, RosterMode = null });

        var result = await _sut.HandleAsync(Query);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RosterMode.Should().Be(RosterMode.Open);
    }
}
