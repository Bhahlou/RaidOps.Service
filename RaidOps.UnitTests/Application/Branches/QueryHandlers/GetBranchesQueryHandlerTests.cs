using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Branches.Queries;
using RaidOps.Application.Implementations.Branches.QueryHandlers;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Branches.QueryHandlers;

public class GetBranchesQueryHandlerTests
{
    private readonly Mock<IBranchRepository>  _branches = new();
    private readonly GetBranchesQueryHandler  _sut;

    private static readonly GetBranchesQuery Query = new();

    public GetBranchesQueryHandlerTests()
    {
        _sut = new GetBranchesQueryHandler(_branches.Object);
    }

    [Fact]
    public async Task HandleAsync_ReturnsMappedBranchDtos()
    {
        _branches.Setup(r => r.GetAllAsync(default))
            .ReturnsAsync(
            [
                MakeBranch(id: 1, name: "Retail",      prefix: "dynamic",         shortCode: "TWW"),
                MakeBranch(id: 2, name: "Classic Era",  prefix: "dynamic-classic1x", shortCode: "Classic"),
            ]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);

        var retail = result.Value.Single(b => b.Id == 1);
        retail.Name.Should().Be("Retail");
        retail.BnetNamespacePrefix.Should().Be("dynamic");
        retail.CurrentExpansionShortCode.Should().Be("TWW");

        var classic = result.Value.Single(b => b.Id == 2);
        classic.CurrentExpansionShortCode.Should().Be("Classic");
    }

    [Fact]
    public async Task HandleAsync_EmptyTable_ReturnsOkWithEmptyCollection()
    {
        _branches.Setup(r => r.GetAllAsync(default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Branch MakeBranch(int id, string name, string prefix, string shortCode) => new()
    {
        Id                  = id,
        Name                = name,
        BnetNamespacePrefix = prefix,
        CurrentExpansionId  = 1,
        CurrentExpansion    = new Expansion { Id = 1, Name = "Full name", ShortCode = shortCode },
    };
}
