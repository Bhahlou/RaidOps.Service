using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Guilds.Settings.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Guilds.Settings.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Guilds.Settings.CommandHandlers;

public class UpdateGuildSettingsCommandHandlerTests
{
    private readonly Mock<IGuildAccessService>          _access   = new();
    private readonly Mock<IGuildsRepository>            _guilds   = new();
    private readonly Mock<IAuditLogService>              _auditLog = new();
    private readonly UpdateGuildSettingsCommandHandler  _sut;

    private const string GuildId     = "guild-1";
    private const string RequesterId = "user-1";

    private static readonly UpdateGuildSettingsCommand Command = new()
    {
        GuildId            = GuildId,
        RequesterDiscordId = RequesterId,
        Timezone           = "Europe/Paris",
        Language           = "en",
    };

    public UpdateGuildSettingsCommandHandlerTests()
    {
        _sut = new UpdateGuildSettingsCommandHandler(_access.Object, _guilds.Object, _auditLog.Object, NullLogger<UpdateGuildSettingsCommandHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_RequesterNotMember_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.None);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_RequesterNotAdmin_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_GuildNotFound_ReturnsGuildNotFound()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync((Guild?)null);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildNotFound);
    }

    [Fact]
    public async Task HandleAsync_GuildNotRegistered_ReturnsGuildNotRegistered()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = false });

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildNotRegistered);
    }

    [Fact]
    public async Task HandleAsync_Success_ReturnsOkAndCallsUpdateSettings()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true, Language = "en" });
        _guilds.Setup(g => g.UpdateSettingsAsync(GuildId, Command.Timezone, Command.Language, default))
            .ReturnsAsync(true);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _guilds.Verify(g => g.UpdateSettingsAsync(GuildId, Command.Timezone, Command.Language, default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Success_FirstTimeConfiguration_OmitsOldValues()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true, Timezone = null, Language = null });

        await _sut.HandleAsync(Command);

        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.SettingsUpdated,
            It.Is<Dictionary<string, string>>(v =>
                v["newTimezone"] == "Europe/Paris" && v["newLanguage"] == "en" &&
                !v.ContainsKey("oldTimezone") && !v.ContainsKey("oldLanguage")),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Success_OnlyTimezoneChanged_SetsChangedFieldsToTimezoneOnly()
    {
        var command = new UpdateGuildSettingsCommand
        {
            GuildId = GuildId, RequesterDiscordId = RequesterId,
            Timezone = "Europe/London", Language = "en",
        };
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true, Timezone = "Europe/Paris", Language = command.Language });

        await _sut.HandleAsync(command);

        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.SettingsUpdated,
            It.Is<Dictionary<string, string>>(v =>
                v["oldTimezone"] == "Europe/Paris" && v["newTimezone"] == "Europe/London" &&
                v["changedFields"] == "timezone" && !v.ContainsKey("oldLanguage") && !v.ContainsKey("newLanguage")),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Success_LanguageChanged_LogsOldAndNewLanguageAndIncludesInChangedFields()
    {
        var command = new UpdateGuildSettingsCommand
        {
            GuildId = GuildId, RequesterDiscordId = RequesterId,
            Timezone = "Europe/Paris", Language = "de",
        };
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true, Timezone = "Europe/Paris", Language = "fr" });

        await _sut.HandleAsync(command);

        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.SettingsUpdated,
            It.Is<Dictionary<string, string>>(v =>
                v["oldLanguage"] == "fr" && v["newLanguage"] == "de" && v["changedFields"] == "language"),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Success_AllChanged_SetsChangedFieldsToBoth()
    {
        var command = new UpdateGuildSettingsCommand
        {
            GuildId = GuildId, RequesterDiscordId = RequesterId,
            Timezone = "Europe/London", Language = "de",
        };
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true, Timezone = "Europe/Paris", Language = "fr" });

        await _sut.HandleAsync(command);

        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.SettingsUpdated,
            It.Is<Dictionary<string, string>>(v => v["changedFields"] == "timezone,language"),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Success_NothingChanged_DoesNotLog()
    {
        var command = new UpdateGuildSettingsCommand
        {
            GuildId = GuildId, RequesterDiscordId = RequesterId,
            Timezone = "Europe/Paris", Language = "en",
        };
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true, Timezone = "Europe/Paris", Language = command.Language });

        var result = await _sut.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        _auditLog.Verify(a => a.LogAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GuildAuditAction>(),
            It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
