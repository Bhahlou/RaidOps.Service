using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Branches.Queries;
using RaidOps.Application.Contracts.Branches.Responses;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Implementations.Dispatching;

namespace RaidOps.UnitTests.Application.Dispatching;

public class QueryDispatcherTests
{
    private readonly Mock<IServiceProvider>                                          _services = new();
    private readonly Mock<IQueryHandlerAsync<GetBranchesQuery, IEnumerable<BranchDto>>> _handler  = new();
    private readonly QueryDispatcher                                                 _sut;

    private static readonly GetBranchesQuery Query = new();

    private static readonly Result<IEnumerable<BranchDto>> OkResult =
        Result<IEnumerable<BranchDto>>.Ok([new BranchDto { Id = 1, Name = "Retail" }]);

    public QueryDispatcherTests()
    {
        _services.Setup(sp => sp.GetService(typeof(IQueryHandlerAsync<GetBranchesQuery, IEnumerable<BranchDto>>)))
            .Returns(_handler.Object);

        _sut = new QueryDispatcher(_services.Object);
    }

    [Fact]
    public async Task DispatchAsync_ResolvesHandlerAndDelegates()
    {
        _handler.Setup(h => h.HandleAsync(Query, default)).ReturnsAsync(OkResult);

        await _sut.DispatchAsync<GetBranchesQuery, IEnumerable<BranchDto>>(Query);

        _handler.Verify(h => h.HandleAsync(Query, default), Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_ReturnsHandlerResult()
    {
        _handler.Setup(h => h.HandleAsync(Query, default)).ReturnsAsync(OkResult);

        var result = await _sut.DispatchAsync<GetBranchesQuery, IEnumerable<BranchDto>>(Query);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(b => b.Id == 1);
    }

    [Fact]
    public async Task DispatchAsync_HandlerNotRegistered_ThrowsInvalidOperationException()
    {
        _services.Setup(sp => sp.GetService(typeof(IQueryHandlerAsync<GetBranchesQuery, IEnumerable<BranchDto>>)))
            .Returns((object?)null);

        var act = () => _sut.DispatchAsync<GetBranchesQuery, IEnumerable<BranchDto>>(Query);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
