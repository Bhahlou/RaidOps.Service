using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Buffs;
using RaidOps.Application.Contracts.Raids.Buffs.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Buffs.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Buffs.CommandHandlers;

/// <summary>Unit tests for <see cref="UpdateRaidBuffDefinitionCommandHandler"/>.</summary>
public class UpdateRaidBuffDefinitionCommandHandlerTests
{
    private readonly Mock<IRaidBuffDefinitionValidationService> _validation = new();
    private readonly Mock<IRaidBuffDefinitionsRepository> _definitions = new();
    private readonly UpdateRaidBuffDefinitionCommandHandler _sut;

    private const int DefinitionId = 42;
    private const int ExpansionId = 12;

    public UpdateRaidBuffDefinitionCommandHandlerTests()
    {
        _validation.Setup(v => v.ValidateAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<RaidBuffDefinitionInput>>(), default)).ReturnsAsync([]);
        _sut = new UpdateRaidBuffDefinitionCommandHandler(_validation.Object, _definitions.Object);
    }

    private static RaidBuffDefinition MakeExisting(int spellId = 16176) => new()
    {
        Id = DefinitionId, ExpansionId = ExpansionId, SpellId = spellId,
        LabelEn = "en", LabelFr = "fr", LabelDe = "de",
    };

    private static RaidBuffDefinitionInput MakeInput(int spellId = 16176) => new()
    {
        SpellId = spellId,
        Scope = RaidBuffScope.Raid,
        Kind = RaidBuffKind.Buff,
        LabelEn = "en",
        LabelFr = "fr",
        LabelDe = "de",
        SortOrder = 0,
        Sources = [new RaidBuffSourceDto { ClassId = 7, SpecId = 264 }],
    };

    private static UpdateRaidBuffDefinitionCommand MakeCommand(RaidBuffDefinitionInput? definition = null) => new()
    {
        Id = DefinitionId,
        Definition = definition ?? MakeInput(),
    };

    [Fact]
    public async Task HandleAsync_DefinitionDoesNotExist_ReturnsNotFoundWithoutValidatingOrWriting()
    {
        _definitions.Setup(d => d.GetByIdAsync(DefinitionId, default)).ReturnsAsync((RaidBuffDefinition?)null);

        var result = await _sut.HandleAsync(MakeCommand(), default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.NotFound);
        _validation.VerifyNoOtherCalls();
        _definitions.Verify(d => d.UpdateAsync(It.IsAny<int>(), It.IsAny<RaidBuffDefinition>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ValidationFails_ReturnsInvalidRequestWithoutWriting()
    {
        _definitions.Setup(d => d.GetByIdAsync(DefinitionId, default)).ReturnsAsync(MakeExisting());
        _validation.Setup(v => v.ValidateAsync(ExpansionId, It.IsAny<IReadOnlyList<RaidBuffDefinitionInput>>(), default)).ReturnsAsync(["bad label."]);

        var result = await _sut.HandleAsync(MakeCommand(), default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidRequest);
        result.Detail.Should().Contain("bad label.");
        _definitions.Verify(d => d.UpdateAsync(It.IsAny<int>(), It.IsAny<RaidBuffDefinition>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ValidatesAgainstTheExistingDefinitionsExpansionNotAnyOther()
    {
        _definitions.Setup(d => d.GetByIdAsync(DefinitionId, default)).ReturnsAsync(MakeExisting());

        await _sut.HandleAsync(MakeCommand(), default);

        _validation.Verify(v => v.ValidateAsync(ExpansionId, It.IsAny<IReadOnlyList<RaidBuffDefinitionInput>>(), default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_NewSpellAlreadyUsedByAnotherDefinitionOnTheSameExpansion_ReturnsInvalidRequestWithoutWriting()
    {
        _definitions.Setup(d => d.GetByIdAsync(DefinitionId, default)).ReturnsAsync(MakeExisting(spellId: 16176));
        _definitions.Setup(d => d.GetBySpellAsync(ExpansionId, 14892, default)).ReturnsAsync(new RaidBuffDefinition { Id = 999, ExpansionId = ExpansionId, SpellId = 14892 });

        var result = await _sut.HandleAsync(MakeCommand(MakeInput(spellId: 14892)), default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidRequest);
        _definitions.Verify(d => d.UpdateAsync(It.IsAny<int>(), It.IsAny<RaidBuffDefinition>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_NewSpellClashesOnlyWithTheDefinitionBeingEditedItself_IsAccepted()
    {
        // Keeping the same spell: GetBySpellAsync legitimately returns the row being edited — that's not a clash.
        _definitions.Setup(d => d.GetByIdAsync(DefinitionId, default)).ReturnsAsync(MakeExisting(spellId: 16176));
        _definitions.Setup(d => d.GetBySpellAsync(ExpansionId, 16176, default)).ReturnsAsync(new RaidBuffDefinition { Id = DefinitionId, ExpansionId = ExpansionId, SpellId = 16176 });
        _definitions.Setup(d => d.UpdateAsync(DefinitionId, It.IsAny<RaidBuffDefinition>(), default)).ReturnsAsync(true);

        var result = await _sut.HandleAsync(MakeCommand(), default);

        result.IsSuccess.Should().BeTrue();
        _definitions.Verify(d => d.UpdateAsync(DefinitionId, It.IsAny<RaidBuffDefinition>(), default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Valid_UpdatesWithTheMappedEntityAndReturnsSuccess()
    {
        _definitions.Setup(d => d.GetByIdAsync(DefinitionId, default)).ReturnsAsync(MakeExisting());
        _definitions.Setup(d => d.GetBySpellAsync(ExpansionId, 16176, default)).ReturnsAsync((RaidBuffDefinition?)null);
        _definitions.Setup(d => d.UpdateAsync(DefinitionId, It.IsAny<RaidBuffDefinition>(), default)).ReturnsAsync(true);

        var result = await _sut.HandleAsync(MakeCommand(MakeInput()), default);

        result.IsSuccess.Should().BeTrue();
        _definitions.Verify(d => d.UpdateAsync(DefinitionId, It.Is<RaidBuffDefinition>(e => e.SpellId == 16176), default), Times.Once);
    }
}
