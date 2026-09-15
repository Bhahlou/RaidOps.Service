using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Attributions.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Attributions.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids.Attributions;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Attributions.CommandHandlers;

/// <summary>
/// Unit tests for <see cref="UpdateGuildAttributionDefinitionCommandHandler"/>.
/// </summary>
public class UpdateGuildAttributionDefinitionCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IGuildAttributionDefinitionsRepository> _definitions = new();
    private readonly Mock<ISpellRepository> _spells = new();
    private readonly Mock<IAuditLogService> _auditLog = new();
    private readonly UpdateGuildAttributionDefinitionCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const string RequesterId = "officer-1";
    private const int DefinitionId = 7;

    private static readonly UpdateGuildAttributionDefinitionCommand Command = new()
    {
        GuildId = GuildId,
        RequesterDiscordId = RequesterId,
        DefinitionId = DefinitionId,
        Label = "Innervate",
        Section = "Personals",
        IsRepeatable = true,
        Cells = [new AttributionCellRequest { Kind = AttributionCellKind.NameSlot }],
    };

    public UpdateGuildAttributionDefinitionCommandHandlerTests()
    {
        _sut = new UpdateGuildAttributionDefinitionCommandHandler(_access.Object, _definitions.Object, _spells.Object, _auditLog.Object);
    }

    private void SetupOfficer() => _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);

    [Fact]
    public async Task HandleAsync_NotOfficer_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
        _definitions.Verify(d => d.UpdateAsync(It.IsAny<GuildAttributionDefinition>(), GuildId, default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_InvalidCells_ReturnsValidationError()
    {
        SetupOfficer();
        var invalidCommand = new UpdateGuildAttributionDefinitionCommand { GuildId = GuildId, RequesterDiscordId = RequesterId, DefinitionId = DefinitionId, Label = "x", Cells = [] };

        var result = await _sut.HandleAsync(invalidCommand);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.NoCellsInDefinition);
    }

    [Fact]
    public async Task HandleAsync_DefinitionNotFound_ReturnsAttributionDefinitionNotFound()
    {
        SetupOfficer();
        _definitions.Setup(d => d.UpdateAsync(It.IsAny<GuildAttributionDefinition>(), GuildId, default)).ReturnsAsync(false);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.AttributionDefinitionNotFound);
        _auditLog.Verify(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GuildAuditAction>(), It.IsAny<Dictionary<string, string>>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Success_UpdatesAndLogs()
    {
        SetupOfficer();
        GuildAttributionDefinition? updated = null;
        _definitions.Setup(d => d.UpdateAsync(It.IsAny<GuildAttributionDefinition>(), GuildId, default))
            .Callback<GuildAttributionDefinition, string, CancellationToken>((d, _, _) => updated = d)
            .ReturnsAsync(true);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        updated.Should().NotBeNull();
        updated!.Id.Should().Be(DefinitionId);
        updated.Label.Should().Be("Innervate");
        updated.Cells.Should().HaveCount(1);

        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.AttributionTemplateUpdated,
            It.Is<Dictionary<string, string>>(v => v["label"] == "Innervate"),
            default), Times.Once);
    }
}
