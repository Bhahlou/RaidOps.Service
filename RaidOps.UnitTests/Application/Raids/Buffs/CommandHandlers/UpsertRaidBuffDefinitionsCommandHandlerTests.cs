using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Buffs;
using RaidOps.Application.Contracts.Raids.Buffs.Commands;
using RaidOps.Application.Contracts.Raids.Buffs.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Buffs.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Buffs.CommandHandlers;

/// <summary>Unit tests for <see cref="UpsertRaidBuffDefinitionsCommandHandler"/>.</summary>
public class UpsertRaidBuffDefinitionsCommandHandlerTests
{
    private readonly Mock<IExpansionRepository> _expansions = new();
    private readonly Mock<IRaidBuffDefinitionValidationService> _validation = new();
    private readonly Mock<IRaidBuffDefinitionsRepository> _definitions = new();
    private readonly UpsertRaidBuffDefinitionsCommandHandler _sut;

    private const int ExpansionId = 12;

    private static readonly int[] ExpectedSpellIds = [16176, 14892];

    public UpsertRaidBuffDefinitionsCommandHandlerTests()
    {
        _expansions.Setup(e => e.GetAllAsync(default)).ReturnsAsync([new Expansion { Id = ExpansionId, Name = "Forever", ShortCode = "Forever" }]);
        _validation.Setup(v => v.ValidateAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<RaidBuffDefinitionInput>>(), default)).ReturnsAsync([]);
        _sut = new UpsertRaidBuffDefinitionsCommandHandler(_expansions.Object, _validation.Object, _definitions.Object);
    }

    private static RaidBuffDefinitionInput MakeDefinition(int spellId = 16176) => new()
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

    private static UpsertRaidBuffDefinitionsCommand MakeCommand(bool pruneMissing = false, params RaidBuffDefinitionInput[] definitions) => new()
    {
        ExpansionId = ExpansionId,
        Definitions = definitions.Length > 0 ? [.. definitions] : [MakeDefinition()],
        PruneMissing = pruneMissing,
    };

    [Fact]
    public async Task HandleAsync_NoDefinitions_ReturnsInvalidRequestWithoutTouchingAnythingElse()
    {
        var command = new UpsertRaidBuffDefinitionsCommand { ExpansionId = ExpansionId, Definitions = [] };

        var result = await _sut.HandleAsync(command, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidRequest);
        _expansions.VerifyNoOtherCalls();
        _definitions.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_ExpansionDoesNotExist_ReturnsNotFoundWithoutValidatingOrWriting()
    {
        _expansions.Setup(e => e.GetAllAsync(default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(MakeCommand(), default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.NotFound);
        _validation.VerifyNoOtherCalls();
        _definitions.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_ValidationFails_ReturnsInvalidRequestWithTheJoinedErrorsWithoutWriting()
    {
        _validation.Setup(v => v.ValidateAsync(ExpansionId, It.IsAny<IReadOnlyList<RaidBuffDefinitionInput>>(), default))
            .ReturnsAsync(["first problem.", "second problem."]);

        var result = await _sut.HandleAsync(MakeCommand(), default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidRequest);
        result.Detail.Should().Contain("first problem.").And.Contain("second problem.");
        _definitions.Verify(d => d.UpsertAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<RaidBuffDefinition>>(), It.IsAny<bool>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Valid_WritesTheMappedEntitiesWithThePruneFlagAndReturnsTheSummary()
    {
        _definitions.Setup(d => d.UpsertAsync(ExpansionId, It.IsAny<IReadOnlyList<RaidBuffDefinition>>(), true, default))
            .ReturnsAsync((2, 1, 3));

        var result = await _sut.HandleAsync(MakeCommand(pruneMissing: true, MakeDefinition(16176), MakeDefinition(14892)), default);

        result.IsSuccess.Should().BeTrue();
        var summary = result.Value.Should().BeOfType<CommandResponse>().Which.Body.Should().BeOfType<RaidBuffUpsertSummary>().Subject;
        summary.Created.Should().Be(2);
        summary.Updated.Should().Be(1);
        summary.Deleted.Should().Be(3);
        _definitions.Verify(d => d.UpsertAsync(
            ExpansionId,
            It.Is<IReadOnlyList<RaidBuffDefinition>>(list => list.Select(x => x.SpellId).SequenceEqual(ExpectedSpellIds)),
            true,
            default), Times.Once);
    }
}
