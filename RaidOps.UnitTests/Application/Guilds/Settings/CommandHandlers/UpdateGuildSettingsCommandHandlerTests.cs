using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Guilds.Settings.Commands;
using RaidOps.Application.Implementations.Guilds.Settings.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Guilds.Settings.CommandHandlers;

public class UpdateGuildSettingsCommandHandlerTests
{
    private readonly Mock<IUserGuildsRepository>        _userGuilds = new();
    private readonly Mock<IGuildsRepository>            _guilds     = new();
    private readonly UpdateGuildSettingsCommandHandler  _sut;

    private const string GuildId     = "guild-1";
    private const string RequesterId = "user-1";

    private static readonly UpdateGuildSettingsCommand Command = new()
    {
        GuildId             = GuildId,
        RequesterDiscordId  = RequesterId,
        Timezone            = "Europe/Paris",
        RosterMode          = RosterMode.Open,
        MinRosterRoleId     = null,
    };

    public UpdateGuildSettingsCommandHandlerTests()
    {
        _sut = new UpdateGuildSettingsCommandHandler(_userGuilds.Object, _guilds.Object);
    }

    [Fact]
    public async Task HandleAsync_RequesterNotMember_ReturnsForbidden()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(RequesterId, default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_RequesterNotAdmin_ReturnsForbidden()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(RequesterId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = RequesterId, IsAdmin = false }]);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_GuildNotFound_ReturnsGuildNotFound()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(RequesterId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = RequesterId, IsAdmin = true }]);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync((Guild?)null);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildNotFound);
    }

    [Fact]
    public async Task HandleAsync_GuildNotRegistered_ReturnsGuildNotRegistered()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(RequesterId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = RequesterId, IsAdmin = true }]);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = false });

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildNotRegistered);
    }

    [Fact]
    public async Task HandleAsync_Success_ReturnsOkAndCallsUpdateSettings()
    {
        _userGuilds.Setup(r => r.GetByUserDiscordIdAsync(RequesterId, default))
            .ReturnsAsync([new UserGuild { GuildId = GuildId, UserDiscordId = RequesterId, IsAdmin = true }]);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true });
        _guilds.Setup(g => g.UpdateSettingsAsync(GuildId, Command.Timezone, Command.RosterMode, null, default))
            .ReturnsAsync(true);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _guilds.Verify(g => g.UpdateSettingsAsync(GuildId, Command.Timezone, Command.RosterMode, null, default), Times.Once);
    }
}
