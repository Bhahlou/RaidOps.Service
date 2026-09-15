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
/// Unit tests for <see cref="CreateGuildAttributionDefinitionCommandHandler"/>.
/// </summary>
public class CreateGuildAttributionDefinitionCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IGuildAttributionDefinitionsRepository> _definitions = new();
    private readonly Mock<ISpellRepository> _spells = new();
    private readonly Mock<IAuditLogService> _auditLog = new();
    private readonly CreateGuildAttributionDefinitionCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const string RequesterId = "officer-1";

    private static readonly CreateGuildAttributionDefinitionCommand Command = new()
    {
        GuildId = GuildId,
        RequesterDiscordId = RequesterId,
        Label = "Innervate",
        Section = "Personals",
        IsRepeatable = true,
        Cells = [new AttributionCellRequest { Kind = AttributionCellKind.NameSlot }],
    };

    public CreateGuildAttributionDefinitionCommandHandlerTests()
    {
        _sut = new CreateGuildAttributionDefinitionCommandHandler(_access.Object, _definitions.Object, _spells.Object, _auditLog.Object);
    }

    private void SetupOfficer() => _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);

    [Fact]
    public async Task HandleAsync_NotOfficer_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
        _definitions.Verify(d => d.AddAsync(It.IsAny<GuildAttributionDefinition>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_InvalidCells_ReturnsValidationError()
    {
        SetupOfficer();
        var invalidCommand = new CreateGuildAttributionDefinitionCommand { GuildId = GuildId, RequesterDiscordId = RequesterId, Label = "x", Cells = [] };

        var result = await _sut.HandleAsync(invalidCommand);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.NoCellsInDefinition);
        _definitions.Verify(d => d.AddAsync(It.IsAny<GuildAttributionDefinition>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Success_AddsDefinitionAndLogs()
    {
        SetupOfficer();
        GuildAttributionDefinition? added = null;
        _definitions.Setup(d => d.AddAsync(It.IsAny<GuildAttributionDefinition>(), default))
            .Callback<GuildAttributionDefinition, CancellationToken>((d, _) => added = d)
            .ReturnsAsync((GuildAttributionDefinition d, CancellationToken _) => d);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        added.Should().NotBeNull();
        added!.GuildId.Should().Be(GuildId);
        added.Label.Should().Be("Innervate");
        added.Section.Should().Be("Personals");
        added.IsRepeatable.Should().BeTrue();
        added.CreatedByDiscordId.Should().Be(RequesterId);
        added.Cells.Should().HaveCount(1);

        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.AttributionTemplateUpdated,
            It.Is<Dictionary<string, string>>(v => v["label"] == "Innervate"),
            default), Times.Once);
    }
}
